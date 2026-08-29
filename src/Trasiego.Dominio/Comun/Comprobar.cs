using System.Runtime.CompilerServices;

namespace Trasiego.Dominio.Comun;

// Estas lanzan ArgumentException y no ExcepcionDeDominio a proposito: no son reglas de
// negocio, son fallos de programacion que no deberian llegar a una pantalla.
public static class Comprobar
{
    public static string NoEnBlanco(
        string? valor,
        [CallerArgumentExpression(nameof(valor))] string? nombre = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, nombre);
        return valor.Trim();
    }

    public static string ComoMucho(
        string valor,
        int longitud,
        [CallerArgumentExpression(nameof(valor))] string? nombre = null)
    {
        if (valor.Length > longitud)
            throw new ArgumentException($"No puede pasar de {longitud} caracteres.", nombre);
        return valor;
    }
}
