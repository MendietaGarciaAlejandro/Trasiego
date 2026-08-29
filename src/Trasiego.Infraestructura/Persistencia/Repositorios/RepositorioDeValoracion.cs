using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeValoracion(ContextoDeTrasiego contexto) : IRepositorioDeValoracion
{
    public void Agregar(CapaDeExistencias capa) => contexto.Capas.Add(capa);

    public void Agregar(ConsumoDeCapa consumo) => contexto.Consumos.Add(consumo);

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

    public async Task<IReadOnlyList<CapaDeExistencias>> CapasPorId(
        IEnumerable<Guid> ids,
        CancellationToken cancelacion = default) =>
        await contexto.Capas.Where(c => ids.Contains(c.Id)).ToListAsync(cancelacion);

    public async Task<IReadOnlyList<ConsumoDeCapa>> ConsumosDe(
        Guid movimientoId,
        CancellationToken cancelacion = default) =>
        // Los ids son version 7, que llevan la hora delante: ordenar por id es ordenar por
        // el momento en que se creo cada consumo, que es el orden en que se vaciaron.
        await contexto.Consumos
            .Where(c => c.MovimientoId == movimientoId)
            .OrderBy(c => c.Id)
            .ToListAsync(cancelacion);

    public async Task<Importe> ValorDeLasExistencias(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        var suma = await contexto.Database
            .SqlQuery<decimal?>($"""
                SELECT SUM(CosteRestante) AS Value
                FROM CapasDeExistencias
                WHERE ArticuloId = {articuloId} AND AlmacenId = {almacenId}
                """)
            .SingleAsync(cancelacion);

        return Importe.De(suma ?? 0m);
    }
}
