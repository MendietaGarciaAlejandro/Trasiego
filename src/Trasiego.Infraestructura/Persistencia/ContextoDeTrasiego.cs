using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Movimientos;

namespace Trasiego.Infraestructura.Persistencia;

public class ContextoDeTrasiego(DbContextOptions<ContextoDeTrasiego> opciones) : DbContext(opciones)
{
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Almacen> Almacenes => Set<Almacen>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(ContextoDeTrasiego).Assembly);
    }
}
