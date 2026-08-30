using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Convertidores;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeDocumento : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> documento)
    {
        documento.ToTable("Documentos");
        documento.HasKey(d => d.Id);

        documento.Property(d => d.Tipo).HasConversion<int>();
        documento.Property(d => d.Estado).HasConversion<int>();
        documento.Property(d => d.Numero).HasMaxLength(40).IsRequired();
        documento.Property(d => d.Concepto).HasMaxLength(200);
        documento.Property(d => d.FechaContable).HasColumnType("date");

        // Un mismo numero puede repetirse entre tipos: el albaran 1042 de entrada y el 1042
        // de salida son papeles distintos y de sitios distintos.
        documento.HasIndex(d => new { d.Tipo, d.Numero }).IsUnique();
        documento.HasIndex(d => new { d.AlmacenId, d.Estado });

        // Las lineas no existen fuera de su documento, asi que se cargan y se borran con el.
        documento.HasMany(d => d.Lineas)
            .WithOne()
            .HasForeignKey(l => l.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        documento.Navigation(d => d.Lineas).AutoInclude();
    }
}

public class ConfiguracionDeLineaDeDocumento : IEntityTypeConfiguration<LineaDeDocumento>
{
    public void Configure(EntityTypeBuilder<LineaDeDocumento> linea)
    {
        linea.ToTable("LineasDeDocumento");
        linea.HasKey(l => l.Id);

        linea.Property(l => l.Cantidad)
            .HasConversion<ConvertidorDeCantidad>().HasPrecision(18, Cantidad.Decimales);
        linea.Property(l => l.Coste)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);

        linea.Property(l => l.Lote).HasMaxLength(40);
        linea.Property(l => l.Caducidad).HasColumnType("date");

        linea.HasIndex(l => new { l.DocumentoId, l.Orden });
    }
}
