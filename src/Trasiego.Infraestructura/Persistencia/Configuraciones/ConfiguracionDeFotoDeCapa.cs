using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Cierres;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Convertidores;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeFotoDeCapa : IEntityTypeConfiguration<FotoDeCapa>
{
    public void Configure(EntityTypeBuilder<FotoDeCapa> foto)
    {
        foto.ToTable("FotosDeCapa");
        foto.HasKey(f => f.Id);

        foto.Property(f => f.Cantidad)
            .HasConversion<ConvertidorDeCantidad>().HasPrecision(18, Cantidad.Decimales);
        foto.Property(f => f.Coste)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);

        foto.Property(f => f.FechaContable).HasColumnType("date");

        foto.HasIndex(f => new { f.CierreId, f.ArticuloId });
        foto.HasIndex(f => new { f.CierreId, f.CapaId }).IsUnique();
    }
}
