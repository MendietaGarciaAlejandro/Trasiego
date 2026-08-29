using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;

namespace Trasiego.Infraestructura.Persistencia;

public class ContextoDeTrasiego(DbContextOptions<ContextoDeTrasiego> opciones) : DbContext(opciones)
{
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Almacen> Almacenes => Set<Almacen>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();
    public DbSet<CapaDeExistencias> Capas => Set<CapaDeExistencias>();
    public DbSet<ConsumoDeCapa> Consumos => Set<ConsumoDeCapa>();
    public DbSet<Descubierto> Descubiertos => Set<Descubierto>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(ContextoDeTrasiego).Assembly);
    }
}
