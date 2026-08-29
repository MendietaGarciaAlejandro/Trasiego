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

    /// <summary>Lo que vale ahora mismo lo que queda en el almacen.</summary>
    Task<Importe> ValorDeLasExistencias(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);
}
