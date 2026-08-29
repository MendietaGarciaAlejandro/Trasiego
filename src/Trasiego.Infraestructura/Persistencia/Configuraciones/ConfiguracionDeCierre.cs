using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Cierres;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Convertidores;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeCierre : IEntityTypeConfiguration<Cierre>
{
    public void Configure(EntityTypeBuilder<Cierre> cierre)
    {
        cierre.ToTable("Cierres");
        cierre.HasKey(c => c.Id);

        cierre.Property(c => c.Hasta).HasColumnType("date");
        cierre.Property(c => c.Concepto).HasMaxLength(200);

        // Dos cierres del mismo almacen hasta el mismo dia no significan nada.
        cierre.HasIndex(c => new { c.AlmacenId, c.Hasta }).IsUnique();
    }
}

public class ConfiguracionDeSaldoDeCierre : IEntityTypeConfiguration<SaldoDeCierre>
{
    public void Configure(EntityTypeBuilder<SaldoDeCierre> saldo)
    {
        saldo.ToTable("SaldosDeCierre");
        saldo.HasKey(s => s.Id);

        saldo.Property(s => s.Cantidad)
            .HasConversion<ConvertidorDeSaldo>().HasPrecision(18, Cantidad.Decimales);
        saldo.Property(s => s.Valor)
            .HasConversion<ConvertidorDeImporte>().HasPrecision(19, Importe.Decimales);

        saldo.HasIndex(s => new { s.CierreId, s.ArticuloId }).IsUnique();
    }
}
