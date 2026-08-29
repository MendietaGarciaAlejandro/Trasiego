using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Almacenes;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeAlmacen : IEntityTypeConfiguration<Almacen>
{
    public void Configure(EntityTypeBuilder<Almacen> almacen)
    {
        almacen.ToTable("Almacenes");
        almacen.HasKey(a => a.Id);

        almacen.Property(a => a.Codigo).HasMaxLength(10).IsRequired();
        almacen.Property(a => a.Nombre).HasMaxLength(200).IsRequired();

        almacen.HasIndex(a => a.Codigo).IsUnique();
    }
}
