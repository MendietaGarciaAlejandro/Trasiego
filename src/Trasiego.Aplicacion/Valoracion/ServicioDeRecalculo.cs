using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Cierres;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;

namespace Trasiego.Aplicacion.Valoracion;

public class ServicioDeRecalculo(
    IRepositorioDeArticulos articulos,
    IRepositorioDeMovimientos movimientos,
    IRepositorioDeValoracion valoracion,
    IRepositorioDeCierres cierres,
    IUnidadDeTrabajo unidadDeTrabajo)
{
    /// <summary>
    /// Reproduce el historico de un articulo desde el ultimo cierre y dice en que se aparta
    /// de lo que hay registrado. No cambia nada: solo mira.
    /// </summary>
    public async Task<Reproduccion> Comparar(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        var (articulo, cierre) = await Contexto(articuloId, almacenId, cancelacion);
        var fotos = await Fotos(cierre, articuloId, cancelacion);

        // Copias sueltas: comparar no debe poder tocar las capas de verdad ni por descuido.
        var apertura = fotos
            .Select(foto => new CapaDeExistencias(
                articuloId, almacenId, Guid.Empty, foto.Cantidad, foto.Coste,
                foto.FechaContable, foto.MomentoDeRegistro))
            .ToList();

        return Recalculo.Reproducir(
            articulo.Metodo, articuloId, almacenId, apertura,
            await movimientos.Listar(articuloId, almacenId, cierre?.Hasta, false, cancelacion));
    }

    /// <summary>
    /// Deshace todo lo que hay por encima del ultimo cierre y lo vuelve a construir en orden.
    /// Corrige el coste de las salidas que valorasen distinto.
    /// </summary>
    public async Task<Reproduccion> Aplicar(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        var (articulo, cierre) = await Contexto(articuloId, almacenId, cancelacion);
        var fotos = await Fotos(cierre, articuloId, cancelacion);
        var desde = cierre?.Hasta ?? DateOnly.MinValue;

        // Primero se tira lo de arriba: las capas que abrieron esos movimientos, lo que
        // consumieron y lo que dejaron a deber.
        await valoracion.Deshacer(articuloId, almacenId, desde, cancelacion);

        // Y las capas que venian de antes vuelven a como estaban el dia del cierre.
        var apertura = await valoracion.CapasPorId(
            fotos.Select(foto => foto.CapaId), cancelacion);

        foreach (var capa in apertura)
        {
            var foto = fotos.Single(f => f.CapaId == capa.Id);
            capa.Restaurar(foto.Cantidad, foto.Coste);
        }

        var enOrden = fotos
            .Select(foto => apertura.Single(capa => capa.Id == foto.CapaId))
            .ToList();

        var historico = await movimientos.Listar(articuloId, almacenId, cierre?.Hasta, true, cancelacion);

        var reproduccion = Recalculo.Reproducir(
            articulo.Metodo, articuloId, almacenId, enOrden, historico);

        foreach (var capa in reproduccion.CapasNuevas) valoracion.Agregar(capa);
        foreach (var consumo in reproduccion.Consumos) valoracion.Agregar(consumo);
        foreach (var descubierto in reproduccion.Descubiertos) valoracion.Agregar(descubierto);

        var porId = historico.ToDictionary(movimiento => movimiento.Id);
        foreach (var salida in reproduccion.Descuadradas)
            porId[salida.MovimientoId].CorregirCoste(salida.Reproducido);

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return reproduccion;
    }

    /// <summary>
    /// Los articulos de un almacen que conviene mirar: los que tienen algun movimiento que
    /// llego con fecha anterior a lo que ya estaba registrado.
    /// </summary>
    public Task<IReadOnlyList<Guid>> ArticulosConRetroactivos(
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        movimientos.ArticulosConRetroactivos(almacenId, cancelacion);

    private async Task<(Articulo Articulo, Cierre? Cierre)> Contexto(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion)
    {
        var articulo = await articulos.PorId(articuloId, cancelacion)
            ?? throw new NoEncontrado("No existe el articulo.");

        return (articulo, await cierres.Ultimo(almacenId, cancelacion));
    }

    private async Task<IReadOnlyList<FotoDeCapa>> Fotos(
        Cierre? cierre,
        Guid articuloId,
        CancellationToken cancelacion) =>
        cierre is null ? [] : await cierres.FotosDe(cierre.Id, articuloId, cancelacion);
}
