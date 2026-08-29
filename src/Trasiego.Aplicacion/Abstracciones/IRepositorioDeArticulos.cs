using Trasiego.Dominio.Catalogo;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeArticulos
{
    Task<Articulo?> PorId(Guid id, CancellationToken cancelacion = default);

    Task<Articulo?> PorReferencia(string referencia, CancellationToken cancelacion = default);

    Task<IReadOnlyList<Articulo>> Listar(bool incluirBajas, CancellationToken cancelacion = default);

    Task Alta(Articulo articulo, CancellationToken cancelacion = default);

    Task GuardarCambios(CancellationToken cancelacion = default);
}
