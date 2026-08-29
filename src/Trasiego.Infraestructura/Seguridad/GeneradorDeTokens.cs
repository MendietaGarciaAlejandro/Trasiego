using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Infraestructura.Seguridad;

public class GeneradorDeTokens(IOptions<OpcionesDeToken> opciones, TimeProvider reloj)
    : IGeneradorDeTokens
{
    private readonly OpcionesDeToken _opciones = opciones.Value;

    public string Para(Usuario usuario)
    {
        var firma = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave)),
            SecurityAlgorithms.HmacSha256);

        // Ocho horas por defecto, que es lo que dura un turno: quien entra por la mañana no
        // tiene que volver a identificarse a media tarde.
        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Correo),
                new Claim(ClaimTypes.Name, usuario.Nombre),
                new Claim(ClaimTypes.Role, usuario.Rol.ToString()),
            ],
            expires: reloj.GetUtcNow().UtcDateTime.AddHours(_opciones.HorasDeValidez),
            signingCredentials: firma);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
