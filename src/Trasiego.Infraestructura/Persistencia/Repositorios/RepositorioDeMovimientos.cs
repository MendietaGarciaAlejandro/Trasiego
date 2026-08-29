using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeMovimientos(ContextoDeTrasiego contexto) : IRepositorioDeMovimientos
{
    public void Agregar(Movimiento movimiento) => contexto.Movimientos.Add(movimiento);

    public Task<Movimiento?> PorId(Guid id, CancellationToken cancelacion = default) =>
        contexto.Movimientos.FirstOrDefaultAsync(m => m.Id == id, cancelacion);

    public async Task<Saldo> SaldoDe(
        Guid articuloId,
        Guid almacenId,
        DateOnly? aFecha = null,
        CancellationToken cancelacion = default)
    {
        // La suma va en SQL a mano. Cantidad se guarda con un ValueConverter, y EF sabe
        // convertir el valor de ida y vuelta pero no sabe sumar el tipo del dominio: para
        // agregar tendria que traerse todos los movimientos y sumarlos en memoria, que es
        // justo lo que no puede hacer un saldo de almacen.
        var suma = await contexto.Database
            .SqlQuery<decimal?>($"""
                SELECT SUM(CASE WHEN Tipo = 1 THEN Cantidad ELSE -Cantidad END) AS Value
                FROM Movimientos
                WHERE ArticuloId = {articuloId}
                  AND AlmacenId = {almacenId}
                  AND ({aFecha} IS NULL OR FechaContable <= {aFecha})
                """)
            .SingleAsync(cancelacion);

        return Saldo.De(suma ?? 0m);
    }

    public async Task<Importe> CosteNeto(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        var suma = await contexto.Database
            .SqlQuery<decimal?>($"""
                SELECT SUM(CASE WHEN Tipo = 1 THEN Coste ELSE -Coste END) AS Value
                FROM Movimientos
                WHERE ArticuloId = {articuloId} AND AlmacenId = {almacenId}
                """)
            .SingleAsync(cancelacion);

        return Importe.De(suma ?? 0m);
    }

    public async Task<DateOnly?> UltimaFechaContable(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        await contexto.Movimientos
            .Where(m => m.ArticuloId == articuloId && m.AlmacenId == almacenId)
            .OrderByDescending(m => m.FechaContable)
            .Select(m => (DateOnly?)m.FechaContable)
            .FirstOrDefaultAsync(cancelacion);

    public Task<bool> TieneMovimientos(Guid articuloId, CancellationToken cancelacion = default) =>
        contexto.Movimientos.AnyAsync(m => m.ArticuloId == articuloId, cancelacion);

    public async Task<IReadOnlyList<Movimiento>> Listar(
        Guid articuloId,
        Guid almacenId,
        DateOnly? despuesDe = null,
        bool conSeguimiento = false,
        CancellationToken cancelacion = default)
    {
        // Normalmente esto se usa para mirar, asi que va sin seguimiento. Solo lo pide el
        // recalculo cuando ademas va a corregir el coste de alguna salida.
        var consulta = contexto.Movimientos
            .Where(m => m.ArticuloId == articuloId && m.AlmacenId == almacenId);

        if (!conSeguimiento) consulta = consulta.AsNoTracking();

        if (despuesDe is { } fecha) consulta = consulta.Where(m => m.FechaContable > fecha);

        return await consulta
            .OrderBy(m => m.FechaContable)
            .ThenBy(m => m.MomentoDeRegistro)
            .ThenBy(m => m.Id)
            .ToListAsync(cancelacion);
    }

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

    public async Task<IReadOnlyList<Movimiento>> TraspasosAlimentadosPor(
        IEnumerable<Guid> salidaIds,
        bool conSeguimiento = false,
        CancellationToken cancelacion = default)
    {
        // Con seguimiento solo cuando hay que corregirles el coste; para mirar, sin el.
        var consulta = contexto.Movimientos
            .Where(m => m.Motivo == MotivoDeMovimiento.Traspaso
                     && m.Tipo == TipoDeMovimiento.Entrada
                     && m.MovimientoOrigenId != null
                     && salidaIds.Contains(m.MovimientoOrigenId.Value));

        if (!conSeguimiento) consulta = consulta.AsNoTracking();

        return await consulta.ToListAsync(cancelacion);
    }

    public async Task<IReadOnlyList<Guid>> ArticulosConRetroactivos(
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        await contexto.Movimientos
            .Where(m => m.AlmacenId == almacenId && m.Retroactivo)
            .Select(m => m.ArticuloId)
            .Distinct()
            .ToListAsync(cancelacion);
}
