using Trasiego.Dominio.Comun;

namespace Trasiego.Dominio.Acceso;

/// <summary>
/// Quien usa el sistema. No guarda la contraseña sino su huella: quien la calcula es la capa
/// de infraestructura, porque el algoritmo con el que se resume es una decision de esa capa
/// y va a cambiar antes que ninguna regla de almacen.
/// </summary>
public class Usuario(string correo, string nombre, string huellaDeLaContrasena, RolDeUsuario rol)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Correo { get; private set; } =
        Comprobar.ComoMucho(Comprobar.NoEnBlanco(correo), 200).ToLowerInvariant();

    public string Nombre { get; private set; } =
        Comprobar.ComoMucho(Comprobar.NoEnBlanco(nombre), 200);

    public string HuellaDeLaContrasena { get; private set; } =
        Comprobar.NoEnBlanco(huellaDeLaContrasena);

    public RolDeUsuario Rol { get; private set; } = rol;

    public bool Activo { get; private set; } = true;

    public void CambiarContrasena(string huella) =>
        HuellaDeLaContrasena = Comprobar.NoEnBlanco(huella);

    public void DarDeBaja()
    {
        if (!Activo) throw new Conflicto($"{Correo} ya estaba de baja.");
        Activo = false;
    }
}
