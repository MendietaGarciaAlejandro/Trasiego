using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Convertidores;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeMovimiento : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> movimiento)
    {
        movimiento.ToTable("Movimientos");
        movimiento.HasKey(m => m.Id);

        movimiento.Property(m => m.Tipo).HasConversion<int>();
        movimiento.Property(m => m.Motivo).HasConversion<int>();

        // Indice filtrado: los retroactivos son pocos y se buscan solos, para poder listar
        // que articulos no valorarian igual si se recalcularan.
        movimiento.HasIndex(m => new { m.ArticuloId, m.AlmacenId })
            .HasFilter("Retroactivo = 1")
            .HasDatabaseName("IX_Movimientos_Retroactivos");
        movimiento.Property(m => m.Concepto).HasMaxLength(200);

        movimiento.Property(m => m.Cantidad)
            .HasConversion<ConvertidorDeCantidad>()
            .HasPrecision(18, Cantidad.Decimales);

        movimiento.Property(m => m.Coste)
            .HasConversion<ConvertidorDeImporte>()
            .HasPrecision(19, Importe.Decimales);

        // date y datetimeoffset, dos columnas de tipos distintos aposta: asi no hay manera
        // de usar una donde toca la otra sin que el compilador se queje.
        movimiento.Property(m => m.FechaContable).HasColumnType("date");

        movimiento.HasOne<Articulo>()
            .WithMany()
            .HasForeignKey(m => m.ArticuloId)
            .OnDelete(DeleteBehavior.Restrict);

        movimiento.HasOne<Almacen>()
            .WithMany()
            .HasForeignKey(m => m.AlmacenId)
            .OnDelete(DeleteBehavior.Restrict);

        // El saldo siempre se pregunta por articulo y almacen, y muchas veces acotado a una
        // fecha, asi que el indice va en ese orden.
        movimiento.HasIndex(m => new { m.ArticuloId, m.AlmacenId, m.FechaContable });
        movimiento.HasIndex(m => m.DocumentoId);
    }
}
