using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Almacenes;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeAlmacenes(ContextoDeTrasiego contexto) : IRepositorioDeAlmacenes
{
    public Task<Almacen?> PorId(Guid id, CancellationToken cancelacion = default) =>
        contexto.Almacenes.FirstOrDefaultAsync(a => a.Id == id, cancelacion);

    public Task<Almacen?> PorCodigo(string codigo, CancellationToken cancelacion = default)
    {
        var buscado = codigo.Trim().ToUpperInvariant();
        return contexto.Almacenes.FirstOrDefaultAsync(a => a.Codigo == buscado, cancelacion);
    }

    public async Task<IReadOnlyList<Almacen>> Listar(
        bool incluirBajas,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Almacenes.AsQueryable();
        if (!incluirBajas) consulta = consulta.Where(a => a.Activo);

        return await consulta.OrderBy(a => a.Codigo).ToListAsync(cancelacion);
    }

    public async Task Alta(Almacen almacen, CancellationToken cancelacion = default)
    {
        contexto.Almacenes.Add(almacen);
        await contexto.SaveChangesAsync(cancelacion);
    }

    public Task GuardarCambios(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
