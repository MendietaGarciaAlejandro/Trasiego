using Trasiego.Dominio.Cierres;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeCierres
{
    void Agregar(Cierre cierre);

    void Agregar(SaldoDeCierre saldo);

    void Agregar(FotoDeCapa foto);

    /// <summary>Como estaban las capas de un articulo el dia del cierre.</summary>
    Task<IReadOnlyList<FotoDeCapa>> FotosDe(
        Guid cierreId,
        Guid articuloId,
        CancellationToken cancelacion = default);

    /// <summary>El cierre mas reciente de un almacen, o nada si no se ha cerrado nunca.</summary>
    Task<Cierre?> Ultimo(Guid almacenId, CancellationToken cancelacion = default);

    Task<Cierre?> PorId(Guid id, CancellationToken cancelacion = default);

    /// <summary>Lo que se declaro en un cierre.</summary>
    Task<IReadOnlyList<SaldoDeCierre>> SaldosDe(
        Guid cierreId,
        CancellationToken cancelacion = default);

    /// <summary>Los cierres de un almacen, del mas reciente al mas antiguo.</summary>
    Task<IReadOnlyList<Cierre>> DeAlmacen(Guid almacenId, CancellationToken cancelacion = default);
}
