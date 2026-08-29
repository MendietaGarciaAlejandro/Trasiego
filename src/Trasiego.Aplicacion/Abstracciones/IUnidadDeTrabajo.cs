namespace Trasiego.Aplicacion.Abstracciones;

/// <summary>
/// Confirma de una vez todo lo que se ha ido preparando. Hace falta desde que existen las
/// capas: una salida toca el movimiento, varias capas y sus consumos, y si eso se guardara
/// por partes un fallo a mitad dejaria el almacen descuadrado.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambios(CancellationToken cancelacion = default);

    /// <summary>
    /// Ejecuta una operacion y la repite entera si otro se ha adelantado. Repetirla entera y
    /// no solo el guardado es lo unico que vale: si otra salida se ha llevado las existencias
    /// entre medias, hay que volver a mirar cuanto queda y de que capas sale.
    /// </summary>
    Task<T> ConReintentos<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancelacion = default);
}
