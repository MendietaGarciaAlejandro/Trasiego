using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Convertidores;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeCapaDeExistencias : IEntityTypeConfiguration<CapaDeExistencias>
{
    public void Configure(EntityTypeBuilder<CapaDeExistencias> capa)
    {
        capa.ToTable("CapasDeExistencias");
        capa.HasKey(c => c.Id);

        capa.Property(c => c.CantidadInicial)
            .HasConversion<ConvertidorDeCantidad>().HasPrecision(18, Cantidad.Decimales);
        capa.Property(c => c.CantidadRestante)
            .HasConversion<ConvertidorDeCantidad>().HasPrecision(18, Cantidad.Decimales);

        capa.Property(c => c.CosteInicial)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);
        capa.Property(c => c.CosteRestante)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);

        capa.Property(c => c.FechaContable).HasColumnType("date");

        // Indice filtrado: las capas agotadas se quedan para poder explicar el historico,
        // pero al buscar de donde sacar una salida solo estorban.
        capa.HasIndex(c => new { c.ArticuloId, c.AlmacenId, c.FechaContable, c.MomentoDeRegistro })
            .HasFilter("CantidadRestante > 0");
    }
}
