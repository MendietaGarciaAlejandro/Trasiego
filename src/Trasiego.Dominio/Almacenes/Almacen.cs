using Trasiego.Dominio.Comun;

namespace Trasiego.Dominio.Almacenes;

public class Almacen(string codigo, string nombre)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Codigo { get; private set; } =
        Comprobar.ComoMucho(Comprobar.NoEnBlanco(codigo), 10).ToUpperInvariant();

    public string Nombre { get; private set; } =
        Comprobar.ComoMucho(Comprobar.NoEnBlanco(nombre), 200);

    public bool Activo { get; private set; } = true;

    public void Renombrar(string nombre) =>
        Nombre = Comprobar.ComoMucho(Comprobar.NoEnBlanco(nombre), 200);

    public void DarDeBaja()
    {
        if (!Activo) throw new Conflicto($"El almacen {Codigo} ya estaba de baja.");
        Activo = false;
    }
}
