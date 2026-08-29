using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Convertidores;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeDescubierto : IEntityTypeConfiguration<Descubierto>
{
    public void Configure(EntityTypeBuilder<Descubierto> descubierto)
    {
        descubierto.ToTable("Descubiertos");
        descubierto.HasKey(d => d.Id);

        descubierto.Property(d => d.Cantidad)
            .HasConversion<ConvertidorDeCantidad>().HasPrecision(18, Cantidad.Decimales);
        descubierto.Property(d => d.CantidadCubierta)
            .HasConversion<ConvertidorDeCantidad>().HasPrecision(18, Cantidad.Decimales);

        descubierto.Property(d => d.Coste)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);
        descubierto.Property(d => d.CosteCubierto)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);

        descubierto.HasIndex(d => new { d.ArticuloId, d.AlmacenId });
        descubierto.HasIndex(d => d.MovimientoId);
    }
}
