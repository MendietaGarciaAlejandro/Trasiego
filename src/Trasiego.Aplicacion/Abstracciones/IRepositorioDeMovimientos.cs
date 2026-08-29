using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Abstracciones;

/// <summary>Una fila del recuento que sale de sumar movimientos hasta una fecha.</summary>
public record SaldoCalculado(Guid ArticuloId, decimal Cantidad, decimal Valor);

public interface IRepositorioDeMovimientos
{
    void Agregar(Movimiento movimiento);

    Task<Movimiento?> PorId(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Lo que hay de un articulo en un almacen. Con <paramref name="aFecha"/> se pregunta
    /// por lo que habia al cierre de ese dia contable.
    /// </summary>
    Task<Saldo> SaldoDe(
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

    /// <summary>La fecha contable mas reciente que hay registrada, para saber si algo llega tarde.</summary>
    Task<DateOnly?> UltimaFechaContable(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default);

    Task<bool> TieneMovimientos(Guid articuloId, CancellationToken cancelacion = default);

    /// <summary>
    /// Los movimientos en el orden en que cuentan: por fecha contable, y a igualdad de
    /// fecha, por el momento en que se registraron. Con <paramref name="despuesDe"/> se pide
    /// solo lo que hay por encima de esa fecha, que es lo que se puede reproducir.
    /// </summary>
    Task<IReadOnlyList<Movimiento>> Listar(
        Guid articuloId,
        Guid almacenId,
        DateOnly? despuesDe = null,
        bool conSeguimiento = false,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Lo que dicen los movimientos que habia en un almacen a una fecha, articulo a articulo.
    /// Como cada movimiento lleva su coste, esto ya es la valoracion de ese dia.
    /// </summary>
    Task<IReadOnlyList<SaldoCalculado>> SaldosA(
        Guid almacenId,
        DateOnly fecha,
        CancellationToken cancelacion = default);

    /// <summary>Los articulos de un almacen que tienen algun movimiento que llego tarde.</summary>
    Task<IReadOnlyList<Guid>> ArticulosConRetroactivos(
        Guid almacenId,
        CancellationToken cancelacion = default);
}
