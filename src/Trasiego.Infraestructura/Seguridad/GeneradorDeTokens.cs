using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

    public TimeSpan LoQueDuraLaRenovacion => TimeSpan.FromDays(_opciones.DiasDeRenovacion);

    public string DeAcceso(Usuario usuario)
    {
        var firma = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave)),
            SecurityAlgorithms.HmacSha256);

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
            expires: reloj.GetUtcNow().UtcDateTime.AddMinutes(_opciones.MinutosDeAcceso),
            signingCredentials: firma);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string Token, string Huella) DeRenovacion()
    {
        // Treinta y dos bytes de azar. No lleva nada dentro que haya que leer: solo tiene que
        // ser imposible de acertar.
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (token, HuellaDe(token));
    }

    // SHA-256 y no BCrypt como en las contraseñas: BCrypt va lento aposta porque una
    // contraseña la elige una persona y se puede probar a adivinar. Esto son 32 bytes de
    // azar, asi que no hay nada que adivinar y solo hace falta no guardarlo en claro.
    public string HuellaDe(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
