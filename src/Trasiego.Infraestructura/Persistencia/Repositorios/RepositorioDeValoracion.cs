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
