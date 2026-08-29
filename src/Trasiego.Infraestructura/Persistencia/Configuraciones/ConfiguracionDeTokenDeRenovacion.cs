using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeTokenDeRenovacion : IEntityTypeConfiguration<TokenDeRenovacion>
{
    public void Configure(EntityTypeBuilder<TokenDeRenovacion> token)
    {
        token.ToTable("Renovaciones");
        token.HasKey(t => t.Id);

        // La huella es un SHA-256 en hexadecimal: siempre 64 caracteres.
        token.Property(t => t.Huella).HasMaxLength(64).IsRequired();

        token.HasIndex(t => t.Huella).IsUnique();
        token.HasIndex(t => t.UsuarioId);
    }
}
