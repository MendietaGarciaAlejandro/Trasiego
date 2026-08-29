namespace Trasiego.Aplicacion.Abstracciones;

/// <summary>
/// Confirma de una vez todo lo que se ha ido preparando. Hace falta desde que existen las
/// capas: una salida toca el movimiento, varias capas y sus consumos, y si eso se guardara
/// por partes un fallo a mitad dejaria el almacen descuadrado.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambios(CancellationToken cancelacion = default);
}
