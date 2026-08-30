using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeValoracion(ContextoDeTrasiego contexto) : IRepositorioDeValoracion
{
    public void Agregar(CapaDeExistencias capa) => contexto.Capas.Add(capa);

    public void Agregar(ConsumoDeCapa consumo) => contexto.Consumos.Add(consumo);

    public void Agregar(Descubierto descubierto) => contexto.Descubiertos.Add(descubierto);

    public async Task<IReadOnlyList<CapaDeExistencias>> CapasConExistencias(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        await contexto.Capas
            .Where(c => c.ArticuloId == articuloId && c.AlmacenId == almacenId)
            .Where(c => c.CantidadRestante != Cantidad.Cero)
            .OrderBy(c => c.FechaContable)
            .ThenBy(c => c.MomentoDeRegistro)
            .ThenBy(c => c.Id)
            .ToListAsync(cancelacion);

    public async Task<CapaDeExistencias?> CapaAbierta(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        await contexto.Capas
            .Where(c => c.ArticuloId == articuloId && c.AlmacenId == almacenId)
            .Where(c => c.CantidadRestante != Cantidad.Cero)
            .OrderBy(c => c.FechaContable)
            .ThenBy(c => c.MomentoDeRegistro)
            .ThenBy(c => c.Id)
            .FirstOrDefaultAsync(cancelacion);

    public async Task<IReadOnlyList<CapaDeExistencias>> Lotes(
        Guid almacenId,
        DateOnly? caducanAntesDe = null,
        CancellationToken cancelacion = default) =>
        await contexto.Capas
            .Where(c => c.AlmacenId == almacenId && c.CantidadRestante != Cantidad.Cero)
            .Where(c => caducanAntesDe == null
                     || (c.Caducidad != null && c.Caducidad < caducanAntesDe))
            // En el mismo orden en que se consumirian: lo que antes caduca, primero.
            .OrderBy(c => c.Caducidad ?? DateOnly.MaxValue)
            .ThenBy(c => c.FechaContable)
            .ThenBy(c => c.MomentoDeRegistro)
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<CapaDeExistencias>> CapasConExistenciasDelAlmacen(
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        await contexto.Capas
            .Where(c => c.AlmacenId == almacenId && c.CantidadRestante != Cantidad.Cero)
            .OrderBy(c => c.ArticuloId)
            .ThenBy(c => c.FechaContable)
            .ToListAsync(cancelacion);

    public Task<bool> HayDescubiertosPendientes(
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        contexto.Descubiertos.AnyAsync(
            d => d.AlmacenId == almacenId && d.CantidadCubierta != d.Cantidad, cancelacion);

    public async Task Deshacer(
        Guid articuloId,
        Guid almacenId,
        DateOnly despuesDe,
        CancellationToken cancelacion = default)
    {
        // Los movimientos por debajo del cierre no se pueden tocar, asi que todo lo que hay
        // que deshacer cuelga de movimientos con fecha contable posterior.
        var deArriba = contexto.Movimientos
            .Where(m => m.ArticuloId == articuloId
                     && m.AlmacenId == almacenId
                     && m.FechaContable > despuesDe)
            .Select(m => m.Id);

        await contexto.Consumos
            .Where(c => deArriba.Contains(c.MovimientoId))
            .ExecuteDeleteAsync(cancelacion);

        await contexto.Descubiertos
            .Where(d => deArriba.Contains(d.MovimientoId))
            .ExecuteDeleteAsync(cancelacion);

        await contexto.Capas
            .Where(c => c.ArticuloId == articuloId
                     && c.AlmacenId == almacenId
                     && c.FechaContable > despuesDe)
            .ExecuteDeleteAsync(cancelacion);

        // Los borrados en bloque van directos a la base de datos y no pasan por el
        // seguimiento, asi que lo que quedara cargado en memoria ya no existe. Se olvida
        // todo para que lo que venga despues se lea de nuevo.
        contexto.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<CapaDeExistencias>> CapasPorId(
        IEnumerable<Guid> ids,
        CancellationToken cancelacion = default) =>
        await contexto.Capas.Where(c => ids.Contains(c.Id)).ToListAsync(cancelacion);

    public async Task<IReadOnlyList<ConsumoDeCapa>> ConsumosDe(
        Guid movimientoId,
        CancellationToken cancelacion = default) =>
        await contexto.Consumos
            .Where(c => c.MovimientoId == movimientoId)
            .OrderBy(c => c.Orden)
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<Descubierto>> DescubiertosPendientes(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        await contexto.Descubiertos
            .Where(d => d.ArticuloId == articuloId && d.AlmacenId == almacenId)
            .Where(d => d.CantidadCubierta != d.Cantidad)
            .OrderBy(d => d.Id)
            .ToListAsync(cancelacion);

    public async Task<decimal?> UltimoCosteUnitario(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        await contexto.Database
            .SqlQuery<decimal?>($"""
                SELECT TOP 1 CosteInicial / CantidadInicial AS Value
                FROM CapasDeExistencias
                WHERE ArticuloId = {articuloId}
                  AND AlmacenId = {almacenId}
                  AND CantidadInicial > 0
                ORDER BY FechaContable DESC, MomentoDeRegistro DESC, Id DESC
                """)
            .SingleOrDefaultAsync(cancelacion);

    public async Task<Importe> ValorDeLasExistencias(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        // Las capas menos lo que resten los descubiertos. Si esto no restara, un almacen que
        // ha servido sin tener valdria lo mismo que uno que no ha servido nada, y la
        // invariante dejaria de cuadrar en cuanto alguien sirviera en descubierto.
        var suma = await contexto.Database
            .SqlQuery<decimal?>($"""
                SELECT
                    ISNULL((SELECT SUM(CosteRestante) FROM CapasDeExistencias
                            WHERE ArticuloId = {articuloId} AND AlmacenId = {almacenId}), 0)
                  - ISNULL((SELECT SUM(Coste - CosteCubierto) FROM Descubiertos
                            WHERE ArticuloId = {articuloId} AND AlmacenId = {almacenId}), 0)
                  AS Value
                """)
            .SingleAsync(cancelacion);

        return Importe.De(suma ?? 0m);
    }
}
