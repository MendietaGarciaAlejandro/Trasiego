using Trasiego.Dominio.Comun;

namespace Trasiego.Dominio.Catalogo;

public class Articulo(string referencia, string nombre, UnidadDeMedida unidad)
{
    // Version 7 en vez de la 4 de siempre: lleva la marca de tiempo delante, asi que los
    // ids salen casi ordenados y SQL Server no anda partiendo paginas del indice agrupado
    // en cada alta.
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Referencia { get; private set; } =
        Comprobar.ComoMucho(Comprobar.NoEnBlanco(referencia), 40).ToUpperInvariant();

    public string Nombre { get; private set; } =
        Comprobar.ComoMucho(Comprobar.NoEnBlanco(nombre), 200);

    public UnidadDeMedida Unidad { get; private set; } = unidad;

    public bool Activo { get; private set; } = true;

    public void Renombrar(string nombre) =>
        Nombre = Comprobar.ComoMucho(Comprobar.NoEnBlanco(nombre), 200);

    // No se borra un articulo que ya tiene movimientos: el historico de valoracion dejaria
    // de poder explicarse. Se da de baja y deja de poder usarse en movimientos nuevos.
    public void DarDeBaja()
    {
        if (!Activo) throw new Conflicto($"El articulo {Referencia} ya estaba de baja.");
        Activo = false;
    }
}
