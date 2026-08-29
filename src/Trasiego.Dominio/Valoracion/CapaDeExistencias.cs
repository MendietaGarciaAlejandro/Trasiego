using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Valoracion;

/// <summary>
/// Lo que queda de una entrada concreta. Cada entrada abre una capa con su cantidad y su
/// coste, y las salidas van vaciando capas por orden de antiguedad.
/// </summary>
public class CapaDeExistencias
{
    // EF entra por aqui y rellena las propiedades una a una. Hace falta porque el otro
    // constructor recibe una cantidad y un coste que van a parar a dos columnas cada uno
    // (la inicial y la que queda), y por el nombre no hay manera de saber cual es cual.
    private CapaDeExistencias() { }

    public CapaDeExistencias(
        Guid articuloId,
        Guid almacenId,
        Guid movimientoDeEntradaId,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        DateTimeOffset momentoDeRegistro)
    {
        Id = Guid.CreateVersion7();
        ArticuloId = articuloId;
        AlmacenId = almacenId;
        MovimientoDeEntradaId = movimientoDeEntradaId;
        FechaContable = fechaContable;
        MomentoDeRegistro = momentoDeRegistro;

        CantidadInicial = cantidad;
        CantidadRestante = cantidad;
        CosteInicial = coste;
        CosteRestante = coste;
    }

    public Guid Id { get; private set; }

    public Guid ArticuloId { get; private set; }
    public Guid AlmacenId { get; private set; }
    public Guid MovimientoDeEntradaId { get; private set; }

    // Las dos fechas de la entrada que abrio la capa, copiadas aqui porque son las que
    // deciden el orden en que se consume y no apetece ir a buscarlas al movimiento cada vez.
    public DateOnly FechaContable { get; private set; }
    public DateTimeOffset MomentoDeRegistro { get; private set; }

    public Cantidad CantidadInicial { get; private set; }
    public Importe CosteInicial { get; private set; }

    public Cantidad CantidadRestante { get; private set; }
    public Importe CosteRestante { get; private set; }

    public bool Agotada => CantidadRestante.EsCero;

    /// <summary>Saca cantidad de la capa y devuelve lo que vale lo que sale.</summary>
    public Importe Consumir(Cantidad cuanto)
    {
        if (cuanto > CantidadRestante)
            throw new Conflicto(
                $"No se pueden sacar {cuanto} de una capa que tiene {CantidadRestante}.");

        var loQueSale = CosteRestante.Proporcion(cuanto, CantidadRestante);

        CantidadRestante -= cuanto;
        CosteRestante -= loQueSale;     // restando: lo que queda es exacto por construccion

        return loQueSale;
    }
}
