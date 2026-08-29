namespace Trasiego.Dominio.Valores;

/// <summary>
/// Un importe en euros. Con signo: una regularizacion a la baja resta valor al almacen.
/// </summary>
public readonly record struct Importe : IComparable<Importe>
{
    // Se guardan cuatro decimales aunque se enseñen dos. Tres unidades que costaron 10,00 €
    // salen a 3,333333... cada una, y si eso se redondea a dos antes de seguir operando la
    // diferencia se acumula hasta que el valor del almacen deja de cuadrar con la suma de
    // sus movimientos. Por lo mismo no se guarda ningun coste unitario: se guarda cantidad
    // y valor total, y el unitario se saca cuando toca enseñarlo.
    public const int Decimales = 4;
    public const int DecimalesVisibles = 2;

    public decimal Valor { get; }

    private Importe(decimal valor) => Valor = valor;

    public static readonly Importe Cero = new(0m);

    // Redondeo comercial, no el bancario que trae .NET de serie: 0,125 son 0,13, que es lo
    // que espera quien mira una factura.
    public static Importe De(decimal valor) =>
        new(Math.Round(valor, Decimales, MidpointRounding.AwayFromZero));

    public bool EsCero => Valor == 0m;

    /// <summary>El importe tal y como sale en pantalla.</summary>
    public decimal Visible => Math.Round(Valor, DecimalesVisibles, MidpointRounding.AwayFromZero);

    public static Importe operator +(Importe a, Importe b) => new(a.Valor + b.Valor);
    public static Importe operator -(Importe a, Importe b) => new(a.Valor - b.Valor);

    public static bool operator <(Importe a, Importe b) => a.Valor < b.Valor;
    public static bool operator >(Importe a, Importe b) => a.Valor > b.Valor;
    public static bool operator <=(Importe a, Importe b) => a.Valor <= b.Valor;
    public static bool operator >=(Importe a, Importe b) => a.Valor >= b.Valor;

    public int CompareTo(Importe otro) => Valor.CompareTo(otro.Valor);

    /// <summary>
    /// La parte de este importe que corresponde a <paramref name="parte"/> sobre un total.
    /// El resto hay que sacarlo restando, nunca pidiendo la otra proporcion: 1/3 y 2/3 de
    /// 10,00 € calculados por separado pueden sumar 9,9999.
    /// </summary>
    public Importe Proporcion(Cantidad parte, Cantidad total)
    {
        if (total.EsCero)
            throw new ArgumentOutOfRangeException(
                nameof(total), "No se puede repartir un importe sobre una cantidad cero.");

        if (parte > total)
            throw new ArgumentOutOfRangeException(
                nameof(parte), parte.Valor, $"La parte ({parte}) no cabe en el total ({total}).");

        // Multiplicar antes de dividir, para redondear una sola vez y al final.
        return De(Valor * parte.Valor / total.Valor);
    }

    /// <summary>
    /// Coste unitario. Devuelve un <see cref="decimal"/> y no un <see cref="Importe"/>
    /// aposta: es un numero para enseñar, no para guardar ni para seguir operando con el.
    /// </summary>
    public decimal PorUnidad(Cantidad cantidad) =>
        cantidad.EsCero
            ? throw new ArgumentOutOfRangeException(
                nameof(cantidad), "No hay coste unitario de una cantidad cero.")
            : Valor / cantidad.Valor;

    public override string ToString() => Valor.ToString("0.####");
}
