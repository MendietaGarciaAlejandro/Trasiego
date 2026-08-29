using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;

namespace Trasiego.Infraestructura.Persistencia;

public class ContextoDeTrasiego(DbContextOptions<ContextoDeTrasiego> opciones) : DbContext(opciones)
{
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Almacen> Almacenes => Set<Almacen>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(ContextoDeTrasiego).Assembly);
    }
}
