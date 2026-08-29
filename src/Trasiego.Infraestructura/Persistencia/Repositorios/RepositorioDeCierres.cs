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

    public async Task<IReadOnlyList<Cierre>> DeAlmacen(
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        await contexto.Cierres
            .Where(c => c.AlmacenId == almacenId)
            .OrderByDescending(c => c.Hasta)
            .ToListAsync(cancelacion);
}
