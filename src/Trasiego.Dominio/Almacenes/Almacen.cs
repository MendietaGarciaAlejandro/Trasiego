using Trasiego.Dominio.Comun;

namespace Trasiego.Dominio.Almacenes;

public class Almacen(string codigo, string nombre, bool permiteDescubierto = false)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Codigo { get; private set; } =
        Comprobar.ComoMucho(Comprobar.NoEnBlanco(codigo), 10).ToUpperInvariant();

    public string Nombre { get; private set; } =
        Comprobar.ComoMucho(Comprobar.NoEnBlanco(nombre), 200);

    // Servir sin tener genero es una decision del almacen, no del articulo: una obra puede
    // gastar material que todavia no se ha dado de alta, y una tienda no.
    public bool PermiteDescubierto { get; private set; } = permiteDescubierto;

    public bool Activo { get; private set; } = true;

    public void Renombrar(string nombre) =>
        Nombre = Comprobar.ComoMucho(Comprobar.NoEnBlanco(nombre), 200);

    public void DarDeBaja()
    {
        if (!Activo) throw new Conflicto($"El almacen {Codigo} ya estaba de baja.");
        Activo = false;
    }
}
