using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Trasiego.Aplicacion.Abstracciones;

namespace Trasiego.Api.Seguridad;

/// <summary>
/// Saca del token quien esta haciendo la peticion.
/// </summary>
/// <remarks>
/// Del token y no de nada que mande el cliente. Un identificador de usuario que viajara en
/// el cuerpo de la peticion lo podria cambiar cualquiera, y entonces la firma del movimiento
/// no valdria para nada: firmaria quien dijera el que la manda.
/// </remarks>
public class QuienRegistraLaPeticion(IHttpContextAccessor peticiones) : IQuienRegistra
{
    public Guid? Id =>
        Guid.TryParse(
            peticiones.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out var id)
            ? id
            : null;
}
