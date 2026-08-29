namespace Trasiego.Dominio.Acceso;

public enum RolDeUsuario
{
    /// <summary>Mueve mercancia y consulta. Es el dia a dia del almacen.</summary>
    Operario = 1,

    /// <summary>
    /// Ademas cuadra inventarios, cierra periodos, recalcula y toca el catalogo. Son las
    /// operaciones de las que no se vuelve o que cambian lo que ya estaba contado.
    /// </summary>
    Responsable = 2,
}

public static class Roles
{
    public const string Operario = nameof(RolDeUsuario.Operario);
    public const string Responsable = nameof(RolDeUsuario.Responsable);
}
