using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeMovimientos
{
    void Agregar(Movimiento movimiento);

    Task<Movimiento?> PorId(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Lo que hay de un articulo en un almacen. Con <paramref name="aFecha"/> se pregunta
    /// por lo que habia al cierre de ese dia contable.
    /// </summary>
    Task<Cantidad> Saldo(
        Guid articuloId,
        Guid almacenId,
        DateOnly? aFecha = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Lo que ha entrado menos lo que ha salido, en dinero. Es la otra mitad de la
    /// invariante: tiene que dar lo mismo que el valor de las capas que quedan.
    /// </summary>
    Task<Importe> CosteNeto(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);

    Task<bool> TieneMovimientos(Guid articuloId, CancellationToken cancelacion = default);

    /// <summary>
    /// Los movimientos en el orden en que cuentan: por fecha contable, y a igualdad de
    /// fecha, por el momento en que se registraron.
    /// </summary>
    Task<IReadOnlyList<Movimiento>> Listar(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);
}
