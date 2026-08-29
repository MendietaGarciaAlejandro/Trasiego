using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Valoracion;

/// <summary>
/// Mercancia que salio sin estar. Es lo contrario de una capa: en vez de existencias con un
/// coste, es una deuda de existencias con un coste provisional, y resta del valor del
/// almacen hasta que llega la entrada que la cubre.
/// </summary>
public class Descubierto
{
    private Descubierto() { }

    public Descubierto(
        Guid articuloId,
        Guid almacenId,
        Guid movimientoId,
        Cantidad cantidad,
        Importe coste)
    {
        Id = Guid.CreateVersion7();
        ArticuloId = articuloId;
        AlmacenId = almacenId;
        MovimientoId = movimientoId;
        Cantidad = cantidad;
        Coste = coste;
    }

    public Guid Id { get; private set; }

    public Guid ArticuloId { get; private set; }
    public Guid AlmacenId { get; private set; }

    /// <summary>La salida que se sirvio sin tener genero.</summary>
    public Guid MovimientoId { get; private set; }

    public Cantidad Cantidad { get; private set; }

    /// <summary>
    /// Lo que se decidio que valia cuando salio, al ultimo coste que se conocia. Es
    /// provisional en el sentido de que nadie sabia lo que iba a costar de verdad, pero no
    /// se toca despues: lo ya valorado no se revisa.
    /// </summary>
    public Importe Coste { get; private set; }

    public Cantidad CantidadCubierta { get; private set; } = Cantidad.Cero;
    public Importe CosteCubierto { get; private set; } = Importe.Cero;

    public Cantidad SinCubrir => Cantidad - CantidadCubierta;
    public Importe CosteSinCubrir => Coste - CosteCubierto;

    public bool Saldado => SinCubrir.EsCero;

    /// <summary>
    /// Tapa parte del descubierto y devuelve el valor que deja de restar del almacen.
    /// </summary>
    public Importe Cubrir(Cantidad cuanta)
    {
        if (cuanta > SinCubrir)
            throw new Conflicto(
                $"El descubierto es de {SinCubrir}, no se pueden cubrir {cuanta}.");

        // Igual que en capas y consumos: se calcula sobre lo que queda y el resto se resta,
        // para que cubrirlo a plazos cancele exactamente lo que se habia apuntado.
        var cancelado = CosteSinCubrir.Proporcion(cuanta, SinCubrir);

        CantidadCubierta += cuanta;
        CosteCubierto += cancelado;

        return cancelado;
    }
}
