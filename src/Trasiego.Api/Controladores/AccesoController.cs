using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trasiego.Aplicacion.Acceso;
using Trasiego.Contratos;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Api.Controladores;

[ApiController]
[Route("api/acceso")]
[Authorize(Roles = Roles.Responsable)]
public class AccesoController(ServicioDeAcceso acceso) : ControllerBase
{
    /// <summary>Identificarse. Devuelve el token que hay que mandar en lo demas.</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<EntradaVista> Entrar(AccesoPedido peticion, CancellationToken cancelacion)
    {
        var entrada = await acceso.Entrar(peticion.Correo, peticion.Contrasena, cancelacion);
        return new EntradaVista(entrada.Token, entrada.Nombre, entrada.Rol);
    }

    [HttpGet("usuarios")]
    public async Task<IReadOnlyList<UsuarioVisto>> Usuarios(CancellationToken cancelacion) =>
        [.. (await acceso.Listar(cancelacion)).Select(UsuarioVisto.De)];

    [HttpPost("usuarios")]
    public async Task<UsuarioVisto> Alta(AltaDeUsuario peticion, CancellationToken cancelacion) =>
        UsuarioVisto.De(await acceso.Alta(
            peticion.Correo, peticion.Nombre, peticion.Contrasena, peticion.Rol, cancelacion));
}
