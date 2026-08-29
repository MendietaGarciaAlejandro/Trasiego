namespace Trasiego.Dominio.Catalogo;

public enum UnidadDeMedida
{
    Unidad = 1,
    Caja = 2,
    Kilogramo = 3,
    Litro = 4,
    Metro = 5,
}

public static class UnidadesDeMedida
{
    public static string Abreviatura(this UnidadDeMedida unidad) => unidad switch
    {
        UnidadDeMedida.Unidad => "ud",
        UnidadDeMedida.Caja => "caja",
        UnidadDeMedida.Kilogramo => "kg",
        UnidadDeMedida.Litro => "l",
        UnidadDeMedida.Metro => "m",
        _ => throw new ArgumentOutOfRangeException(nameof(unidad)),
    };
}
