namespace Trasiego.Dominio.Valores;

/// <summary>
/// Lo que hay de un articulo en un almacen. A diferencia de <see cref="Cantidad"/> lleva
/// signo, porque un almacen que sirve mercancia antes de registrar la compra se queda en
/// negativo, y eso es un dato que hay que poder enseñar.
/// </summary>
public readonly record struct Saldo : IComparable<Saldo>
{
    public decimal Valor { get; }

    private Saldo(decimal valor) => Valor = valor;

    public static readonly Saldo Cero = new(0m);

    public static Saldo De(decimal valor) =>
        new(Math.Round(valor, Cantidad.Decimales, MidpointRounding.AwayFromZero));

    public static Saldo De(Cantidad cantidad) => new(cantidad.Valor);

    public bool EsCero => Valor == 0m;
    public bool EnDescubierto => Valor < 0m;

    /// <summary>Lo que hay de verdad. Un saldo en descubierto no tiene nada.</summary>
    public Cantidad Disponible => Valor <= 0m ? Cantidad.Cero : Cantidad.De(Valor);

    public static bool operator <(Saldo a, Cantidad b) => a.Valor < b.Valor;
    public static bool operator >(Saldo a, Cantidad b) => a.Valor > b.Valor;
    public static bool operator <(Cantidad a, Saldo b) => a.Valor < b.Valor;
    public static bool operator >(Cantidad a, Saldo b) => a.Valor > b.Valor;

    public static bool operator ==(Saldo a, Cantidad b) => a.Valor == b.Valor;
    public static bool operator !=(Saldo a, Cantidad b) => a.Valor != b.Valor;

    public int CompareTo(Saldo otro) => Valor.CompareTo(otro.Valor);

    public override string ToString() => Valor.ToString("0.####");
}
