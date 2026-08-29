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

        // Marca de version, en propiedad en la sombra para no meter ruido de persistencia en
        // el dominio. Sin esto, dos salidas a la vez leen la misma capa, las dos descuentan
        // sobre lo que leyeron y la segunda escritura pisa a la primera: el mismo genero sale
        // dos veces. Con ella la segunda choca y se reintenta desde el principio.
        capa.Property<byte[]>("Version").IsRowVersion();

        // Indice filtrado: las capas agotadas se quedan para poder explicar el historico,
        // pero al buscar de donde sacar una salida solo estorban.
        capa.HasIndex(c => new { c.ArticuloId, c.AlmacenId, c.FechaContable, c.MomentoDeRegistro })
            .HasFilter("CantidadRestante > 0");
    }
}
