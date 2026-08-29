namespace Trasiego.Dominio.Movimientos;

/// <summary>
/// Por que se movio la mercancia. No cambia como se valora, pero sin esto un almacen es una
/// lista de numeros que nadie sabe explicar cuando no cuadra el inventario.
/// </summary>
public enum MotivoDeMovimiento
{
    Ordinario = 1,

    /// <summary>Lo que dice el recuento no era lo que decia el sistema.</summary>
    Regularizacion = 2,

    /// <summary>Vuelve material que ya habia salido, al coste al que salio.</summary>
    Devolucion = 3,

    /// <summary>
    /// Mercancia que cambia de almacen. Son dos movimientos, la salida de uno y la entrada
    /// en el otro, y la entrada apunta a la salida para que se sepa que van juntos.
    /// </summary>
    Traspaso = 4,
}
