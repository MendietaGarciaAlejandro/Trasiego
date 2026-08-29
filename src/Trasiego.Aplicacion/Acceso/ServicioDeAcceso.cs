using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Comun;

namespace Trasiego.Aplicacion.Acceso;

public record Entrada(string Token, string Nombre, RolDeUsuario Rol);

public class ServicioDeAcceso(
    IRepositorioDeUsuarios usuarios,
    IHuellaDeContrasenas huellas,
    IGeneradorDeTokens tokens)
{
    public async Task<Entrada> Entrar(
        string correo,
        string contrasena,
        CancellationToken cancelacion = default)
    {
        var usuario = await usuarios.PorCorreo(correo.Trim().ToLowerInvariant(), cancelacion);

        // El mismo aviso si el correo no existe que si la contraseña no vale. Decir cual de
        // las dos cosa falla es decirle a quien lo intenta que ese correo existe.
        if (usuario is null
            || !usuario.Activo
            || !huellas.Coincide(contrasena, usuario.HuellaDeLaContrasena))
            throw new NoAutorizado("El correo o la contraseña no son correctos.");

        return new Entrada(tokens.Para(usuario), usuario.Nombre, usuario.Rol);
    }

    public async Task<Usuario> Alta(
        string correo,
        string nombre,
        string contrasena,
        RolDeUsuario rol,
        CancellationToken cancelacion = default)
    {
        var usuario = new Usuario(correo, nombre, huellas.Calcular(contrasena), rol);

        if (await usuarios.PorCorreo(usuario.Correo, cancelacion) is not null)
            throw new Conflicto($"Ya hay alguien dado de alta con {usuario.Correo}.");

        await usuarios.Alta(usuario, cancelacion);
        return usuario;
    }

    public Task<IReadOnlyList<Usuario>> Listar(CancellationToken cancelacion = default) =>
        usuarios.Listar(cancelacion);
}
