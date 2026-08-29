using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Catalogo;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeArticulos(ContextoDeTrasiego contexto) : IRepositorioDeArticulos
{
    public Task<Articulo?> PorId(Guid id, CancellationToken cancelacion = default) =>
        contexto.Articulos.FirstOrDefaultAsync(a => a.Id == id, cancelacion);

    public Task<Articulo?> PorReferencia(string referencia, CancellationToken cancelacion = default)
    {
        var buscada = referencia.Trim().ToUpperInvariant();
        return contexto.Articulos.FirstOrDefaultAsync(a => a.Referencia == buscada, cancelacion);
    }

    public async Task<IReadOnlyList<Articulo>> Listar(
        bool incluirBajas,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Articulos.AsQueryable();
        if (!incluirBajas) consulta = consulta.Where(a => a.Activo);

        return await consulta.OrderBy(a => a.Referencia).ToListAsync(cancelacion);
    }

    public async Task Alta(Articulo articulo, CancellationToken cancelacion = default)
    {
        contexto.Articulos.Add(articulo);
        await contexto.SaveChangesAsync(cancelacion);
    }

    public Task GuardarCambios(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
