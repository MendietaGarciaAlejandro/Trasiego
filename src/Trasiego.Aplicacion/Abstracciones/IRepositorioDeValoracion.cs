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

    /// <summary>Lo que vale ahora mismo lo que queda en el almacen.</summary>
    Task<Importe> ValorDeLasExistencias(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);
}
