using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Cierres;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Infraestructura.Persistencia;

public class ContextoDeTrasiego(DbContextOptions<ContextoDeTrasiego> opciones) : DbContext(opciones)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<TokenDeRenovacion> Renovaciones => Set<TokenDeRenovacion>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Almacen> Almacenes => Set<Almacen>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();
    public DbSet<CapaDeExistencias> Capas => Set<CapaDeExistencias>();
    public DbSet<ConsumoDeCapa> Consumos => Set<ConsumoDeCapa>();
    public DbSet<Descubierto> Descubiertos => Set<Descubierto>();
    public DbSet<Cierre> Cierres => Set<Cierre>();
    public DbSet<SaldoDeCierre> SaldosDeCierre => Set<SaldoDeCierre>();
    public DbSet<FotoDeCapa> FotosDeCapa => Set<FotoDeCapa>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(ContextoDeTrasiego).Assembly);

        // Los ids los ponemos nosotros, siempre Guid version 7, y nunca la base de datos.
        // Decirselo importa: si EF cree que los genera el, una entidad nueva que aparece
        // dentro de otra (una linea dentro de su documento) le parece una que ya existia por
        // traer id, e intenta actualizar una fila que no esta.
        foreach (var clave in modelo.Model.GetEntityTypes()
                     .SelectMany(tipo => tipo.GetProperties())
                     .Where(propiedad => propiedad.IsPrimaryKey()
                                      && propiedad.ClrType == typeof(Guid)))
            clave.ValueGenerated = ValueGenerated.Never;

        // No es una tabla, es el resultado de un group by. Se declara aqui para poder
        // pedirlo con FromSql y que EF sepa materializarlo. La precision se pone a mano
        // porque sin ella EF avisa de que podria truncar al leer, y son las mismas columnas
        // de Movimientos sumadas.
        modelo.Entity<SaldoCalculado>(saldo =>
        {
            saldo.HasNoKey().ToView(null);
            saldo.Property(s => s.Cantidad).HasPrecision(18, Cantidad.Decimales);
            saldo.Property(s => s.Valor).HasPrecision(19, Importe.Decimales);
        });
    }
}
