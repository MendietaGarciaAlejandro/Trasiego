using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Comun;

namespace Trasiego.Aplicacion.Acceso;

/// <summary>
/// Lo que se lleva quien acaba de entrar. La renovacion viaja aparte, en una cookie que el
/// guion de la pagina no puede leer.
/// </summary>
public record Entrada(string Token, string Renovacion, string Nombre, RolDeUsuario Rol);

public class ServicioDeAcceso(
    IRepositorioDeUsuarios usuarios,
    IRepositorioDeTokens renovaciones,
    IHuellaDeContrasenas huellas,
    IGeneradorDeTokens tokens,
    TimeProvider reloj)
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

        return await Emitir(usuario, cancelacion);
    }

    /// <summary>
    /// Cambia una renovacion por un token de acceso nuevo, y por otra renovacion: la que se
    /// trae queda gastada.
    /// </summary>
    public async Task<Entrada> Renovar(string renovacion, CancellationToken cancelacion = default)
    {
        var guardada = await renovaciones.PorHuella(tokens.HuellaDe(renovacion), cancelacion)
            ?? throw new NoAutorizado("Esa sesion ya no vale.");

        // Una renovacion gastada que vuelve a aparecer significa que alguien tiene una copia:
        // o la nuestra o la suya. No hay forma de saber cual, asi que se tiran todas y que
        // vuelva a entrar quien sepa la contraseña.
        if (guardada.Usado)
        {
            await renovaciones.RevocarLosDe(guardada.UsuarioId, cancelacion);
            throw new NoAutorizado("Esa sesion ya no vale.");
        }

        if (!guardada.Sirve(reloj.GetUtcNow()))
            throw new NoAutorizado("Esa sesion ya no vale.");

        var usuario = await usuarios.PorId(guardada.UsuarioId, cancelacion);
        if (usuario is null || !usuario.Activo)
            throw new NoAutorizado("Esa sesion ya no vale.");

        guardada.Usar();
        return await Emitir(usuario, cancelacion);
    }

    /// <summary>Cierra la sesion tirando todas las renovaciones de ese usuario.</summary>
    public async Task Salir(string renovacion, CancellationToken cancelacion = default)
    {
        var guardada = await renovaciones.PorHuella(tokens.HuellaDe(renovacion), cancelacion);
        if (guardada is null) return;

        await renovaciones.RevocarLosDe(guardada.UsuarioId, cancelacion);
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

    /// <summary>
    /// Tira las renovaciones que ya habian caducado. Si no, la tabla solo crece: cada vez
    /// que alguien entra o renueva se apunta una mas.
    /// </summary>
    public Task<int> LimpiarRenovacionesCaducadas(CancellationToken cancelacion = default) =>
        renovaciones.BorrarCaducadas(reloj.GetUtcNow(), cancelacion);

    private async Task<Entrada> Emitir(Usuario usuario, CancellationToken cancelacion)
    {
        var (renovacion, huella) = tokens.DeRenovacion();

        renovaciones.Agregar(new TokenDeRenovacion(
            usuario.Id, huella, reloj.GetUtcNow() + tokens.LoQueDuraLaRenovacion));

        await renovaciones.GuardarCambios(cancelacion);

        return new Entrada(tokens.DeAcceso(usuario), renovacion, usuario.Nombre, usuario.Rol);
    }
}
