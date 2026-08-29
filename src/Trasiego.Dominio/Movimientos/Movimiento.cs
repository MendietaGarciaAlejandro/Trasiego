using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Movimientos;

public class Movimiento(
    Guid articuloId,
    Guid almacenId,
    TipoDeMovimiento tipo,
    Cantidad cantidad,
    Importe coste,
    DateOnly fechaContable,
    DateTimeOffset momentoDeRegistro,
    string? concepto = null)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid ArticuloId { get; private set; } = articuloId;
    public Guid AlmacenId { get; private set; } = almacenId;

    public TipoDeMovimiento Tipo { get; private set; } = tipo;

    public Cantidad Cantidad { get; private set; } = cantidad.EsCero
        ? throw new ReglaDeNegocio("Un movimiento de cantidad cero no mueve nada.")
        : cantidad;

    // Lo que costo, si es una entrada, o lo que valia lo que salio, si es una salida. En las
    // salidas no lo teclea nadie: sale de las capas que se vacian.
    public Importe Coste { get; private set; } = coste < Importe.Cero
        ? throw new ReglaDeNegocio("El coste de un movimiento no puede ser negativo.")
        : coste;

    // El dia al que pertenece el movimiento. Es lo que manda en los informes y en el saldo
    // a fecha, y no tiene por que ser hoy: un albaran de la semana pasada se registra hoy
    // pero cuenta en su dia.
    public DateOnly FechaContable { get; private set; } = fechaContable;

    // Cuando se tecleo. Solo sirve para saber quien vio que cosa y cuando, y para poder
    // explicar por que un informe de ayer da hoy un numero distinto.
    public DateTimeOffset MomentoDeRegistro { get; private set; } = momentoDeRegistro;

    public string? Concepto { get; private set; } =
        string.IsNullOrWhiteSpace(concepto) ? null : Comprobar.ComoMucho(concepto.Trim(), 200);
}
