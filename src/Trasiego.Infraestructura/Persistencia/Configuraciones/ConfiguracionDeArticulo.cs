using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Catalogo;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeArticulo : IEntityTypeConfiguration<Articulo>
{
    public void Configure(EntityTypeBuilder<Articulo> articulo)
    {
        articulo.ToTable("Articulos");
        articulo.HasKey(a => a.Id);

        articulo.Property(a => a.Referencia).HasMaxLength(40).IsRequired();
        articulo.Property(a => a.Nombre).HasMaxLength(200).IsRequired();
        articulo.Property(a => a.Unidad).HasConversion<int>();
        articulo.Property(a => a.Metodo).HasConversion<int>();

        // La referencia es lo que teclea quien da de alta un movimiento, asi que la unicidad
        // la impone la base de datos y no una consulta previa: dos altas a la vez con la
        // misma referencia pasarian las dos la comprobacion.
        articulo.HasIndex(a => a.Referencia).IsUnique();
    }
}
