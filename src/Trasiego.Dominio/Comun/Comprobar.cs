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

    /// <summary>
    /// Deja un numero de lote como se guarda: sin espacios, en mayusculas y como mucho de
    /// cuarenta. En blanco es nulo, que es lo que significa "este no lleva lote".
    /// </summary>
    /// <remarks>
    /// Va aqui porque lo normalizan la capa, la linea de documento y el que pide un lote al
    /// servir, y si cada uno lo hiciera a su manera "l-01" y "L-01" acabarian siendo lotes
    /// distintos segun por donde entraran.
    /// </remarks>
    public static string? Lote(string? lote) =>
        string.IsNullOrWhiteSpace(lote) ? null : ComoMucho(lote.Trim(), 40).ToUpperInvariant();

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
