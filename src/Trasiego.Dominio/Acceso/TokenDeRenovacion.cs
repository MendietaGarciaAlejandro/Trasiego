using Trasiego.Dominio.Comun;

namespace Trasiego.Dominio.Acceso;

/// <summary>
/// Lo que permite volver a entrar sin teclear la contraseña otra vez. Aqui solo se guarda su
/// huella, igual que con las contraseñas: quien se lleve esta tabla no se lleva ninguna
/// sesion.
/// </summary>
public class TokenDeRenovacion(Guid usuarioId, string huella, DateTimeOffset caduca)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UsuarioId { get; private set; } = usuarioId;

    public string Huella { get; private set; } = Comprobar.NoEnBlanco(huella);

    public DateTimeOffset Caduca { get; private set; } = caduca;

    /// <summary>
    /// Cada renovacion gasta el token y emite otro. Uno gastado que vuelve a aparecer es
    /// señal de que alguien tiene una copia, y entonces se tiran todos los de ese usuario.
    /// </summary>
    public bool Usado { get; private set; }

    public bool Revocado { get; private set; }

    public bool Sirve(DateTimeOffset ahora) => !Usado && !Revocado && ahora < Caduca;

    public void Usar() => Usado = true;

    public void Revocar() => Revocado = true;
}
