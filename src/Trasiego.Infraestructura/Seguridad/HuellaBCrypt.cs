using Trasiego.Aplicacion.Abstracciones;

namespace Trasiego.Infraestructura.Seguridad;

public class HuellaBCrypt : IHuellaDeContrasenas
{
    public string Calcular(string contrasena) => BCrypt.Net.BCrypt.HashPassword(contrasena);

    public bool Coincide(string contrasena, string huella)
    {
        // Una huella corrupta o de otro formato hace saltar a BCrypt. Eso no es motivo para
        // devolver un 500: significa que ese usuario no puede entrar, y punto.
        try
        {
            return BCrypt.Net.BCrypt.Verify(contrasena, huella);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
