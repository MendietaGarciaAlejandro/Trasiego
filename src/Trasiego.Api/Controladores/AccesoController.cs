using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trasiego.Aplicacion.Acceso;
using Trasiego.Contratos;
using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Comun;

namespace Trasiego.Api.Controladores;

[ApiController]
[Route("api/acceso")]
[Authorize(Roles = Roles.Responsable)]
public class AccesoController(ServicioDeAcceso acceso) : ControllerBase
{
    private const string Galleta = "trasiego_renovacion";

    /// <summary>Identificarse. Devuelve el token que hay que mandar en lo demas.</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<EntradaVista> Entrar(AccesoPedido peticion, CancellationToken cancelacion) =>
        Guardar(await acceso.Entrar(peticion.Correo, peticion.Contrasena, cancelacion));

    /// <summary>
    /// Cambia la renovacion por un token de acceso nuevo. No lleva cuerpo: la renovacion va
    /// en la cookie, que el navegador manda sola.
    /// </summary>
    [HttpPost("renovar")]
    [AllowAnonymous]
    public async Task<EntradaVista> Renovar(CancellationToken cancelacion)
    {
        var renovacion = Request.Cookies[Galleta]
            ?? throw new NoAutorizado("Aqui no hay ninguna sesion que renovar.");

        return Guardar(await acceso.Renovar(renovacion, cancelacion));
    }

    /// <summary>Cierra la sesion y se lleva la cookie por delante.</summary>
    [HttpPost("salir")]
    [AllowAnonymous]
    public async Task<NoContentResult> Salir(CancellationToken cancelacion)
    {
        if (Request.Cookies[Galleta] is { } renovacion)
            await acceso.Salir(renovacion, cancelacion);

        Response.Cookies.Delete(Galleta, Opciones());
        return NoContent();
    }

    [HttpGet("usuarios")]
    public async Task<IReadOnlyList<UsuarioVisto>> Usuarios(CancellationToken cancelacion) =>
        [.. (await acceso.Listar(cancelacion)).Select(UsuarioVisto.De)];

    [HttpPost("usuarios")]
    public async Task<UsuarioVisto> Alta(AltaDeUsuario peticion, CancellationToken cancelacion) =>
        UsuarioVisto.De(await acceso.Alta(
            peticion.Correo, peticion.Nombre, peticion.Contrasena, peticion.Rol, cancelacion));

    /// <summary>
    /// Deja la renovacion en la cookie y devuelve solo el token de acceso. La renovacion no
    /// sale nunca en el cuerpo: si saliera, el guion de la pagina podria leerla, y entonces
    /// daria igual que la cookie fuera inaccesible.
    /// </summary>
    private EntradaVista Guardar(Entrada entrada)
    {
        Response.Cookies.Append(Galleta, entrada.Renovacion, Opciones());
        return new EntradaVista(entrada.Token, entrada.Nombre, entrada.Rol);
    }

    private CookieOptions Opciones() => new()
    {
        // Lo importante: ningun guion de la pagina puede leerla, asi que una inyeccion de
        // codigo no se lleva la sesion.
        HttpOnly = true,

        // Solo por conexion segura cuando la hay. En desarrollo se trabaja sobre http y
        // marcarla asi la haria desaparecer.
        Secure = Request.IsHttps,

        // No se manda en peticiones que vengan de otro sitio.
        SameSite = SameSiteMode.Strict,

        // Y solo viaja hacia aqui: en el resto de la Api no pinta nada.
        Path = "/api/acceso",
    };
}
