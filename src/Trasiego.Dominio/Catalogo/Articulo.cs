using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Catalogo;

public class Articulo(
    string referencia,
    string nombre,
    UnidadDeMedida unidad,
    MetodoDeValoracion metodo = MetodoDeValoracion.Fifo,
    bool llevaLotes = false)
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

    // El criterio va por articulo y no por almacen: es lo que exige poder explicar una
    // valoracion, y mover el mismo material de un almacen a otro no puede cambiar lo que
    // vale.
    public MetodoDeValoracion Metodo { get; private set; } = metodo;

    /// <summary>
    /// Si se sigue por lotes. Lo que entra tiene que decir de que lote es, y lo que sale
    /// vacia primero lo que antes caduque.
    /// </summary>
    /// <remarks>
    /// No se puede llevar por lotes y valorar a precio medio a la vez. A precio medio todas
    /// las entradas caen en la capa que ya estaba abierta, y esa capa es justamente lo que
    /// distingue un lote de otro: si se mezclan, no queda donde apuntar de que lote es cada
    /// cosa. Un articulo con lotes se valora por capas.
    /// </remarks>
    public bool LlevaLotes { get; private set; } =
        llevaLotes && metodo is MetodoDeValoracion.PrecioMedio
            ? throw new ReglaDeNegocio(
                "Un articulo con lotes no se puede valorar a precio medio: la capa unica " +
                "mezcla los lotes y ya no se sabe cual sale.")
            : llevaLotes;

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

    /// <summary>
    /// Cambia el criterio de valoracion. Solo mientras el articulo no tenga historico: si ya
    /// se ha valorado una salida con un criterio, cambiarlo deja el almacen contando una
    /// cosa y los movimientos otra.
    /// </summary>
    public void CambiarMetodo(MetodoDeValoracion metodo, bool tieneMovimientos)
    {
        if (tieneMovimientos)
            throw new Conflicto(
                $"{Referencia} ya tiene movimientos: su criterio de valoracion no se toca.");

        if (LlevaLotes && metodo is MetodoDeValoracion.PrecioMedio)
            throw new ReglaDeNegocio(
                $"{Referencia} se lleva por lotes: a precio medio se mezclarian en una sola capa.");

        Metodo = metodo;
    }

    /// <summary>
    /// Comprueba que lo que se declara en una entrada encaja con como se lleva el articulo.
    /// </summary>
    public void ComprobarLote(string? lote)
    {
        if (LlevaLotes && string.IsNullOrWhiteSpace(lote))
            throw new ReglaDeNegocio($"{Referencia} se lleva por lotes: falta decir de cual es.");

        if (!LlevaLotes && !string.IsNullOrWhiteSpace(lote))
            throw new ReglaDeNegocio($"{Referencia} no se lleva por lotes.");
    }

    public void ComprobarCantidad(Cantidad cantidad)
    {
        if (!Unidad.AdmiteDecimales() && cantidad.Valor != Math.Truncate(cantidad.Valor))
            throw new ReglaDeNegocio(
                $"{Referencia} se lleva en {Unidad.EnPlural()}: {cantidad} no es una cantidad valida.");
    }
}
