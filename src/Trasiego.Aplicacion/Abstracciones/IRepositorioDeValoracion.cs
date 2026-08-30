using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeValoracion
{
    void Agregar(CapaDeExistencias capa);

    void Agregar(ConsumoDeCapa consumo);

    /// <summary>Las capas con algo dentro, por orden de antiguedad contable.</summary>
    Task<IReadOnlyList<CapaDeExistencias>> CapasConExistencias(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// La capa que sigue teniendo existencias, o nada si no hay. A precio medio solo puede
    /// haber una, y es a la que van a parar las entradas siguientes.
    /// </summary>
    Task<CapaDeExistencias?> CapaAbierta(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<CapaDeExistencias>> CapasPorId(
        IEnumerable<Guid> ids,
        CancellationToken cancelacion = default);

    /// <summary>De que capas salio una salida, en el mismo orden en que se consumieron.</summary>
    Task<IReadOnlyList<ConsumoDeCapa>> ConsumosDe(
        Guid movimientoId,
        CancellationToken cancelacion = default);

    void Agregar(Descubierto descubierto);

    /// <summary>
    /// Las capas con existencias de un almacen, opcionalmente solo las que caducan antes de
    /// una fecha. Es lo que hay repartido por lotes: una capa ya es un lote.
    /// </summary>
    Task<IReadOnlyList<CapaDeExistencias>> Lotes(
        Guid almacenId,
        DateOnly? caducanAntesDe = null,
        CancellationToken cancelacion = default);

    /// <summary>Todas las capas con existencias de un almacen, para la foto del cierre.</summary>
    Task<IReadOnlyList<CapaDeExistencias>> CapasConExistenciasDelAlmacen(
        Guid almacenId,
        CancellationToken cancelacion = default);

    Task<bool> HayDescubiertosPendientes(
        Guid almacenId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Deshace lo que generaron los movimientos posteriores a una fecha: las capas que se
    /// abrieron, los consumos que hicieron y los descubiertos que dejaron.
    /// </summary>
    Task Deshacer(
        Guid articuloId,
        Guid almacenId,
        DateOnly despuesDe,
        CancellationToken cancelacion = default);

    /// <summary>Los descubiertos por tapar, en el orden en que se produjeron.</summary>
    Task<IReadOnlyList<Descubierto>> DescubiertosPendientes(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El coste por unidad de la ultima entrada que se conoce, para poder valorar lo que
    /// sale sin estar. Nada si por ese almacen no ha pasado todavia ese articulo.
    /// </summary>
    Task<decimal?> UltimoCosteUnitario(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Lo que vale ahora mismo lo que queda en el almacen: las capas menos lo que resten
    /// los descubiertos sin tapar.
    /// </summary>
    Task<Importe> ValorDeLasExistencias(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);
}
