using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Valoracion;

/// <summary>
/// De que capa salio cada trozo de una salida. Sin esto el coste de una salida es un numero
/// sin explicacion, y no hay manera de devolver material al precio al que entro.
/// </summary>
public class ConsumoDeCapa(Guid movimientoId, Guid capaId, Cantidad cantidad, Importe coste)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid MovimientoId { get; private set; } = movimientoId;
    public Guid CapaId { get; private set; } = capaId;

    public Cantidad Cantidad { get; private set; } = cantidad;
    public Importe Coste { get; private set; } = coste;

    public Cantidad CantidadDevuelta { get; private set; } = Cantidad.Cero;
    public Importe CosteDevuelto { get; private set; } = Importe.Cero;

    public Cantidad SinDevolver => Cantidad - CantidadDevuelta;

    /// <summary>
    /// Marca que parte de este consumo vuelve al almacen, y devuelve lo que costo en su dia.
    /// </summary>
    public Importe Devolver(Cantidad cuanta)
    {
        if (cuanta > SinDevolver)
            throw new Conflicto(
                $"De esta salida solo quedan {SinDevolver} por devolver, no {cuanta}.");

        // Mismo apaño que en las capas: se calcula sobre lo que queda por devolver y el
        // resto sale de restar, para que devolver a plazos sume exactamente lo que costo.
        var coste = (Coste - CosteDevuelto).Proporcion(cuanta, SinDevolver);

        CantidadDevuelta += cuanta;
        CosteDevuelto += coste;

        return coste;
    }
}
