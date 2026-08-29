using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeMovimientos(ContextoDeTrasiego contexto) : IRepositorioDeMovimientos
{
    public async Task Alta(Movimiento movimiento, CancellationToken cancelacion = default)
    {
        contexto.Movimientos.Add(movimiento);
        await contexto.SaveChangesAsync(cancelacion);
    }

    public async Task<Cantidad> Saldo(
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

        return Cantidad.De(suma ?? 0m);
    }

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
