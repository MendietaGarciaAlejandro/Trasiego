using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeMovimientos
{
    Task Alta(Movimiento movimiento, CancellationToken cancelacion = default);

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
    /// Los movimientos en el orden en que cuentan: por fecha contable, y a igualdad de
    /// fecha, por el momento en que se registraron.
    /// </summary>
    Task<IReadOnlyList<Movimiento>> Listar(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);
}
