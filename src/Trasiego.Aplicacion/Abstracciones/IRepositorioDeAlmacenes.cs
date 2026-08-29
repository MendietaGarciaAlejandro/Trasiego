using Trasiego.Dominio.Almacenes;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeAlmacenes
{
    Task<Almacen?> PorId(Guid id, CancellationToken cancelacion = default);

    Task<Almacen?> PorCodigo(string codigo, CancellationToken cancelacion = default);

    Task<IReadOnlyList<Almacen>> Listar(bool incluirBajas, CancellationToken cancelacion = default);

    Task Alta(Almacen almacen, CancellationToken cancelacion = default);

    Task GuardarCambios(CancellationToken cancelacion = default);
}
