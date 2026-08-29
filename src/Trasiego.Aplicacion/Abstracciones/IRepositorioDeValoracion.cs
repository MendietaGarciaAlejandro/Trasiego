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
