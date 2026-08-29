using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Cierres;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeCierres(ContextoDeTrasiego contexto) : IRepositorioDeCierres
{
    public void Agregar(Cierre cierre) => contexto.Cierres.Add(cierre);

    public void Agregar(SaldoDeCierre saldo) => contexto.SaldosDeCierre.Add(saldo);

    public void Agregar(FotoDeCapa foto) => contexto.FotosDeCapa.Add(foto);

    public async Task<IReadOnlyList<FotoDeCapa>> FotosDe(
        Guid cierreId,
        Guid articuloId,
        CancellationToken cancelacion = default) =>
        await contexto.FotosDeCapa
            .Where(f => f.CierreId == cierreId && f.ArticuloId == articuloId)
            .OrderBy(f => f.FechaContable)
            .ThenBy(f => f.MomentoDeRegistro)
            .ThenBy(f => f.CapaId)
            .ToListAsync(cancelacion);

    public Task<Cierre?> Ultimo(Guid almacenId, CancellationToken cancelacion = default) =>
        contexto.Cierres
            .Where(c => c.AlmacenId == almacenId)
            .OrderByDescending(c => c.Hasta)
            .FirstOrDefaultAsync(cancelacion);

    public Task<Cierre?> PorId(Guid id, CancellationToken cancelacion = default) =>
        contexto.Cierres.FirstOrDefaultAsync(c => c.Id == id, cancelacion);

    public async Task<IReadOnlyList<SaldoDeCierre>> SaldosDe(
        Guid cierreId,
        CancellationToken cancelacion = default) =>
        await contexto.SaldosDeCierre
            .Where(s => s.CierreId == cierreId)
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<SaldoCalculado>> SaldosA(
        Guid almacenId,
        DateOnly fecha,
        CancellationToken cancelacion = default) =>
        // Un group by y nada mas. Como cada movimiento lleva su coste, sumar hasta una fecha
        // ya da lo que valia el almacen ese dia: no hay que reconstruir capas para saberlo.
        await contexto.Set<SaldoCalculado>()
            .FromSql($"""
                SELECT
                    ArticuloId,
                    SUM(CASE WHEN Tipo = 1 THEN Cantidad ELSE -Cantidad END) AS Cantidad,
                    SUM(CASE WHEN Tipo = 1 THEN Coste ELSE -Coste END) AS Valor
                FROM Movimientos
                WHERE AlmacenId = {almacenId} AND FechaContable <= {fecha}
                GROUP BY ArticuloId
                """)
            .ToListAsync(cancelacion);
}
