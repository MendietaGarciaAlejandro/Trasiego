using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Cierres;

/// <summary>
/// Marca hasta que dia contable esta cerrado un almacen. Por debajo de esa fecha no se
/// registra nada mas, y eso es lo que convierte la valoracion a esa fecha en un dato que ya
/// no puede cambiar.
/// </summary>
/// <remarks>
/// Va por almacen y no de golpe para todos. Aqui no hay contabilidad que obligue a un unico
/// corte: cada almacen se inventaria cuando le toca, y cerrar el de la obra no tiene por que
/// esperar a que alguien cuente el de la tienda.
/// </remarks>
public class Cierre(
    Guid almacenId,
    DateOnly hasta,
    DateTimeOffset momentoDeCierre,
    string? concepto = null)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid AlmacenId { get; private set; } = almacenId;

    public DateOnly Hasta { get; private set; } = hasta;

    public DateTimeOffset MomentoDeCierre { get; private set; } = momentoDeCierre;

    public string? Concepto { get; private set; } =
        string.IsNullOrWhiteSpace(concepto) ? null : concepto.Trim();
}

/// <summary>
/// Como estaba una capa el dia del cierre. Sin esto no se puede reproducir un historico
/// desde el cierre: el saldo agregado dice cuanto habia y cuanto valia, pero no en cuantas
/// capas estaba repartido, y en FIFO eso cambia lo que cuesta la siguiente salida.
/// </summary>
public class FotoDeCapa(
    Guid cierreId,
    Guid capaId,
    Guid articuloId,
    Cantidad cantidad,
    Importe coste,
    DateOnly fechaContable,
    DateTimeOffset momentoDeRegistro)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CierreId { get; private set; } = cierreId;
    public Guid CapaId { get; private set; } = capaId;
    public Guid ArticuloId { get; private set; } = articuloId;

    public Cantidad Cantidad { get; private set; } = cantidad;
    public Importe Coste { get; private set; } = coste;

    public DateOnly FechaContable { get; private set; } = fechaContable;
    public DateTimeOffset MomentoDeRegistro { get; private set; } = momentoDeRegistro;
}

/// <summary>
/// Lo que se declaro que habia de un articulo al cerrar. Se puede volver a calcular sumando
/// movimientos, y por eso mismo se guarda: un cierre que deja de cuadrar con lo que dicen
/// los movimientos es la señal de que alguien ha tocado el pasado.
/// </summary>
public class SaldoDeCierre(Guid cierreId, Guid articuloId, Saldo cantidad, Importe valor)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CierreId { get; private set; } = cierreId;
    public Guid ArticuloId { get; private set; } = articuloId;

    public Saldo Cantidad { get; private set; } = cantidad;
    public Importe Valor { get; private set; } = valor;
}
