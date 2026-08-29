using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Infraestructura.Persistencia.Configuraciones;

public class ConfiguracionDeUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> usuario)
    {
        usuario.ToTable("Usuarios");
        usuario.HasKey(u => u.Id);

        usuario.Property(u => u.Correo).HasMaxLength(200).IsRequired();
        usuario.Property(u => u.Nombre).HasMaxLength(200).IsRequired();
        usuario.Property(u => u.HuellaDeLaContrasena).HasMaxLength(100).IsRequired();
        usuario.Property(u => u.Rol).HasConversion<int>();

        usuario.HasIndex(u => u.Correo).IsUnique();
    }
}
