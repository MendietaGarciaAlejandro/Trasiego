namespace Trasiego.Dominio.Valores;

/// <summary>
/// Una cantidad de articulo, en la unidad de medida del articulo. Nunca negativa.
/// </summary>
public readonly record struct Cantidad : IComparable<Cantidad>
{
    // Cuatro decimales es lo normal en un ERP: deja trabajar en gramos sobre un articulo
    // que se lleva en kilos.
    public const int Decimales = 4;

    public decimal Valor { get; }

    private Cantidad(decimal valor) => Valor = valor;

    public static readonly Cantidad Cero = new(0m);

    public static Cantidad De(decimal valor)
    {
        if (valor < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(valor), valor, "Una cantidad no puede ser negativa.");

        return new Cantidad(Math.Round(valor, Decimales, MidpointRounding.AwayFromZero));
    }

    public bool EsCero => Valor == 0m;

    public static Cantidad operator +(Cantidad a, Cantidad b) => new(a.Valor + b.Valor);

    // Que restar de mas reviente en vez de dar un negativo es lo que impide que una capa
    // de existencias acabe con cantidad negativa por un descuadre. El saldo de un almacen
    // si puede ser negativo, pero eso se lleva aparte y con signo.
    public static Cantidad operator -(Cantidad a, Cantidad b) =>
        b.Valor > a.Valor
            ? throw new ArgumentOutOfRangeException(
                nameof(b), b.Valor, $"No se pueden restar {b} de {a}: quedaria en negativo.")
            : new Cantidad(a.Valor - b.Valor);

    public static bool operator <(Cantidad a, Cantidad b) => a.Valor < b.Valor;
    public static bool operator >(Cantidad a, Cantidad b) => a.Valor > b.Valor;
    public static bool operator <=(Cantidad a, Cantidad b) => a.Valor <= b.Valor;
    public static bool operator >=(Cantidad a, Cantidad b) => a.Valor >= b.Valor;

    public int CompareTo(Cantidad otra) => Valor.CompareTo(otra.Valor);

    public override string ToString() => Valor.ToString("0.####");
}
