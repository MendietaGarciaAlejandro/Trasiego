using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Cierres;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Cierres;

/// <summary>Un articulo que ya no dice lo mismo que decia cuando se cerro.</summary>
public record Descuadre(
    Guid ArticuloId,
    Saldo CantidadDeclarada,
    Saldo CantidadAhora,
    Importe ValorDeclarado,
    Importe ValorAhora);

public class ServicioDeCierres(
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeCierres cierres,
    IRepositorioDeValoracion valoracion,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    /// <summary>
    /// Cierra un almacen hasta un dia contable y deja apuntado lo que habia en ese momento.
    /// </summary>
    public async Task<Cierre> Cerrar(
        Guid almacenId,
        DateOnly hasta,
        string? concepto = null,
        CancellationToken cancelacion = default)
    {
        var almacen = await almacenes.PorId(almacenId, cancelacion)
            ?? throw new NoEncontrado("No existe el almacen.");

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (hasta > hoy)
            throw new ReglaDeNegocio("No se cierra un dia que todavia no ha pasado.");

        var ultimo = await cierres.Ultimo(almacen.Id, cancelacion);
        if (ultimo is not null && hasta <= ultimo.Hasta)
            throw new ReglaDeNegocio(
                $"{almacen.Codigo} ya esta cerrado hasta el {ultimo.Hasta:dd/MM/yyyy}.");

        // Cerrar debiendo genero seria cerrar sobre una suposicion: lo que salio sin estar
        // vale lo que se supuso, y todavia puede resultar que costara otra cosa.
        if (await valoracion.HayDescubiertosPendientes(almacen.Id, cancelacion))
            throw new ReglaDeNegocio(
                $"{almacen.Codigo} debe genero sin recibir: tapa los descubiertos antes de cerrar.");

        var cierre = new Cierre(almacen.Id, hasta, reloj.GetUtcNow(), concepto);
        cierres.Agregar(cierre);

        // Ademas del saldo se guarda como estaban las capas. El saldo dice cuanto habia y
        // cuanto valia; el desglose dice en cuantas capas estaba repartido, y en FIFO eso es
        // lo que decide lo que cuesta la siguiente salida.
        foreach (var capa in await valoracion.CapasConExistenciasDelAlmacen(almacen.Id, cancelacion))
            cierres.Agregar(new FotoDeCapa(
                cierre.Id, capa.Id, capa.ArticuloId,
                capa.CantidadRestante, capa.CosteRestante,
                capa.FechaContable, capa.MomentoDeRegistro));

        // El valor a una fecha es la suma de los movimientos hasta esa fecha, sin reconstruir
        // capas ni nada: como cada movimiento lleva su coste, esto es un group by. Lo que
        // aporta el cierre es la garantia de que esa suma ya no va a cambiar.
        foreach (var saldo in await cierres.SaldosA(almacen.Id, hasta, cancelacion))
            cierres.Agregar(new SaldoDeCierre(
                cierre.Id, saldo.ArticuloId, Saldo.De(saldo.Cantidad), Importe.De(saldo.Valor)));

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return cierre;
    }

    /// <summary>
    /// Vuelve a sumar los movimientos hasta la fecha del cierre y lo compara con lo que se
    /// declaro entonces. Deberia salir vacio siempre; si sale algo, alguien ha tocado el
    /// pasado por debajo del cierre.
    /// </summary>
    public async Task<IReadOnlyList<Descuadre>> Comprobar(
        Guid cierreId,
        CancellationToken cancelacion = default)
    {
        var cierre = await cierres.PorId(cierreId, cancelacion)
            ?? throw new NoEncontrado("No existe ese cierre.");

        var declarados = await cierres.SaldosDe(cierreId, cancelacion);
        var ahora = (await cierres.SaldosA(cierre.AlmacenId, cierre.Hasta, cancelacion))
            .ToDictionary(fila => fila.ArticuloId);

        var descuadres = new List<Descuadre>();

        foreach (var declarado in declarados)
        {
            ahora.Remove(declarado.ArticuloId, out var fila);

            var cantidad = fila is null ? Saldo.Cero : Saldo.De(fila.Cantidad);
            var valor = fila is null ? Importe.Cero : Importe.De(fila.Valor);

            if (cantidad != declarado.Cantidad || valor != declarado.Valor)
                descuadres.Add(new Descuadre(
                    declarado.ArticuloId, declarado.Cantidad, cantidad,
                    declarado.Valor, valor));
        }

        // Lo que sale ahora y no estaba declarado tambien es un descuadre: es un movimiento
        // que se ha colado por debajo de la fecha de cierre.
        descuadres.AddRange(ahora.Values.Select(fila => new Descuadre(
            fila.ArticuloId, Saldo.Cero, Saldo.De(fila.Cantidad),
            Importe.Cero, Importe.De(fila.Valor))));

        return descuadres;
    }
}
