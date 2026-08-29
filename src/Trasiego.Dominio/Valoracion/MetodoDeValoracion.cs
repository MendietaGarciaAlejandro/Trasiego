namespace Trasiego.Dominio.Valoracion;

public enum MetodoDeValoracion
{
    /// <summary>Lo primero que entro es lo primero que sale, cada entrada con su coste.</summary>
    Fifo = 1,

    /// <summary>Todo lo que hay vale lo mismo: la media de lo que costo, pesada por cantidad.</summary>
    PrecioMedio = 2,
}
