using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Movimientos;

public class ServicioDeMovimientos(
    IRepositorioDeArticulos articulos,
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeMovimientos movimientos,
    IRepositorioDeValoracion valoracion,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    /// <summary>
    /// Registra una entrada y abre la capa que las salidas iran vaciando.
    /// </summary>
    /// <param name="coste">
    /// Lo que ha costado la entrada entera, no lo que cuesta cada unidad. Se pide asi
    /// aposta: si se pidiera el precio unitario habria que multiplicarlo por la cantidad y
    /// el redondeo de esa multiplicacion ya no cuadraria con la factura.
    /// </param>
    public async Task<Movimiento> RegistrarEntrada(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default)
    {
        var (articulo, almacen) = await Comprobaciones(
            articuloId, almacenId, cantidad, fechaContable, cancelacion);

        var entrada = new Movimiento(
            articulo.Id, almacen.Id, TipoDeMovimiento.Entrada, cantidad, coste,
            fechaContable, reloj.GetUtcNow(), concepto);

        movimientos.Agregar(entrada);

        valoracion.Agregar(new CapaDeExistencias(
            articulo.Id, almacen.Id, entrada.Id, cantidad, coste,
            fechaContable, entrada.MomentoDeRegistro));

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return entrada;
    }

    /// <summary>
    /// Registra una salida. El coste no se teclea: sale de vaciar capas por antiguedad.
    /// </summary>
    public async Task<Movimiento> RegistrarSalida(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default)
    {
        var (articulo, almacen) = await Comprobaciones(
            articuloId, almacenId, cantidad, fechaContable, cancelacion);

        // Se mira el saldo de hoy y no el de la fecha contable del movimiento: aunque el
        // albaran sea de la semana pasada, la mercancia sale del almacen ahora.
        var hay = await movimientos.Saldo(articulo.Id, almacen.Id, cancelacion: cancelacion);
        if (cantidad > hay)
            throw new ReglaDeNegocio(
                $"No hay bastante {articulo.Referencia} en {almacen.Codigo}: " +
                $"quedan {hay} {articulo.Unidad.Abreviatura()} y se piden {cantidad}.");

        var capas = await valoracion.CapasConExistencias(articulo.Id, almacen.Id, cancelacion);
        var tomas = ValoracionFifo.Consumir(capas, cantidad);

        var coste = tomas.Aggregate(Importe.Cero, (suma, toma) => suma + toma.Coste);

        var salida = new Movimiento(
            articulo.Id, almacen.Id, TipoDeMovimiento.Salida, cantidad, coste,
            fechaContable, reloj.GetUtcNow(), concepto);

        movimientos.Agregar(salida);

        foreach (var toma in tomas)
            valoracion.Agregar(new ConsumoDeCapa(salida.Id, toma.CapaId, toma.Cantidad, toma.Coste));

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return salida;
    }

    private async Task<(Articulo, Almacen)> Comprobaciones(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        CancellationToken cancelacion)
    {
        var articulo = await articulos.PorId(articuloId, cancelacion)
            ?? throw new NoEncontrado("No existe el articulo.");

        if (!articulo.Activo)
            throw new ReglaDeNegocio($"El articulo {articulo.Referencia} esta de baja.");

        var almacen = await almacenes.PorId(almacenId, cancelacion)
            ?? throw new NoEncontrado("No existe el almacen.");

        if (!almacen.Activo)
            throw new ReglaDeNegocio($"El almacen {almacen.Codigo} esta de baja.");

        articulo.ComprobarCantidad(cantidad);

        // El dia contable se compara con el dia local del negocio, no con el UTC: en España
        // entre las 00:00 y las 02:00 el UTC todavia va por el dia anterior, y una entrada
        // tecleada a esa hora se rechazaria por futura sin serlo.
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (fechaContable > hoy)
            throw new ReglaDeNegocio("No se puede registrar un movimiento con fecha futura.");

        return (articulo, almacen);
    }
}
