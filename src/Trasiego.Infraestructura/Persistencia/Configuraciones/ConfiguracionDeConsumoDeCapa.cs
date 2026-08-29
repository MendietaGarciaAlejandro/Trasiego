using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Convertidores;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeConsumoDeCapa : IEntityTypeConfiguration<ConsumoDeCapa>
{
    public void Configure(EntityTypeBuilder<ConsumoDeCapa> consumo)
    {
        consumo.ToTable("ConsumosDeCapa");
        consumo.HasKey(c => c.Id);

        consumo.Property(c => c.Cantidad)
            .HasConversion<ConvertidorDeCantidad>().HasPrecision(18, Cantidad.Decimales);
        consumo.Property(c => c.Coste)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);

        consumo.Property(c => c.CantidadDevuelta)
            .HasConversion<ConvertidorDeCantidad>().HasPrecision(18, Cantidad.Decimales);
        consumo.Property(c => c.CosteDevuelto)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);

        consumo.HasIndex(c => c.MovimientoId);
        consumo.HasIndex(c => c.CapaId);
    }
}
