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
        CancellationToken cancelacion = default) =>
        await contexto.Movimientos
            .Where(m => m.ArticuloId == articuloId && m.AlmacenId == almacenId)
            .OrderBy(m => m.FechaContable)
            .ThenBy(m => m.MomentoDeRegistro)
            .ToListAsync(cancelacion);
}
