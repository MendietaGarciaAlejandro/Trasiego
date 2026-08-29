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

    public static string EnPlural(this UnidadDeMedida unidad) => unidad switch
    {
        UnidadDeMedida.Unidad => "unidades",
        UnidadDeMedida.Caja => "cajas",
        UnidadDeMedida.Kilogramo => "kilogramos",
        UnidadDeMedida.Litro => "litros",
        UnidadDeMedida.Metro => "metros",
        _ => throw new ArgumentOutOfRangeException(nameof(unidad)),
    };

    // Media caja de tornillos no existe, pero medio kilo si. Es la clase de error que si no
    // se corta al entrar acaba saliendo en un inventario con 2,5 unidades de algo.
    public static bool AdmiteDecimales(this UnidadDeMedida unidad) =>
        unidad is not (UnidadDeMedida.Unidad or UnidadDeMedida.Caja);
}
