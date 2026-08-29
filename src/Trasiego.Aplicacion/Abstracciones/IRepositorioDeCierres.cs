using Trasiego.Dominio.Cierres;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeCierres
{
    void Agregar(Cierre cierre);

    void Agregar(SaldoDeCierre saldo);

    /// <summary>El cierre mas reciente de un almacen, o nada si no se ha cerrado nunca.</summary>
    Task<Cierre?> Ultimo(Guid almacenId, CancellationToken cancelacion = default);

    Task<Cierre?> PorId(Guid id, CancellationToken cancelacion = default);

    /// <summary>Lo que se declaro en un cierre.</summary>
    Task<IReadOnlyList<SaldoDeCierre>> SaldosDe(
        Guid cierreId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Lo que dicen los movimientos que habia en un almacen a una fecha, articulo a articulo.
    /// </summary>
    Task<IReadOnlyList<SaldoCalculado>> SaldosA(
        Guid almacenId,
        DateOnly fecha,
        CancellationToken cancelacion = default);
}

/// <summary>Una fila del recuento que sale de sumar movimientos hasta una fecha.</summary>
public record SaldoCalculado(Guid ArticuloId, decimal Cantidad, decimal Valor);
