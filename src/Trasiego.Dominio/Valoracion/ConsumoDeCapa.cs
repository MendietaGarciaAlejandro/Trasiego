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
}
