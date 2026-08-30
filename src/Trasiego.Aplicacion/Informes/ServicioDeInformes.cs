using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Informes;

/// <summary>Lo que habia de un articulo a una fecha, con su nombre para poder leerlo.</summary>
public record LineaDeValoracion(
    Guid ArticuloId,
    string Referencia,
    string Nombre,
    Saldo Cantidad,
    Importe Valor);

/// <summary>Lo que queda de un lote en un almacen, y hasta cuando vale.</summary>
public record LineaDeLote(
    Guid ArticuloId,
    string Referencia,
    string Nombre,
    string? Lote,
    DateOnly? Caducidad,
    Cantidad Cantidad,
    Importe Valor);

public class ServicioDeInformes(
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeArticulos articulos,
    IRepositorioDeMovimientos movimientos,
    IRepositorioDeValoracion valoracion)
{
    /// <summary>
    /// Lo que valia un almacen un dia concreto, articulo a articulo.
    /// </summary>
    /// <remarks>
    /// Esto no reconstruye nada: como cada movimiento lleva su coste, sumar los movimientos
    /// hasta esa fecha ya es la valoracion de ese dia. Si la fecha esta por debajo del ultimo
    /// cierre, ademas es un numero que ya no puede cambiar.
    /// </remarks>
    public async Task<IReadOnlyList<LineaDeValoracion>> ValoracionA(
        Guid almacenId,
        DateOnly fecha,
        CancellationToken cancelacion = default)
    {
        _ = await almacenes.PorId(almacenId, cancelacion)
            ?? throw new NoEncontrado("No existe el almacen.");

        var porId = (await articulos.Listar(incluirBajas: true, cancelacion))
            .ToDictionary(articulo => articulo.Id);

        return
        [
            .. (await movimientos.SaldosA(almacenId, fecha, cancelacion))
                // Un articulo que quedo a cero de cantidad y de valor no dice nada; uno que
                // quedo a cero de cantidad pero con valor si, que es la diferencia que deja
                // un descubierto tapado por encima o por debajo de lo que costo.
                .Where(fila => fila.Cantidad != 0m || fila.Valor != 0m)
                .Select(fila => new LineaDeValoracion(
                    fila.ArticuloId,
                    porId[fila.ArticuloId].Referencia,
                    porId[fila.ArticuloId].Nombre,
                    Saldo.De(fila.Cantidad),
                    Importe.De(fila.Valor)))
                .OrderBy(linea => linea.Referencia)
        ];
    }

    /// <summary>
    /// Lo que hay en un almacen repartido por lotes, en el orden en que va a ir saliendo.
    /// Con una fecha, solo lo que caduca antes de ella.
    /// </summary>
    /// <remarks>
    /// Esto tampoco reconstruye nada: una capa de existencias ya era un lote, con su cantidad,
    /// su coste y su fecha. Solo hubo que ponerle el numero y hasta cuando vale.
    /// </remarks>
    public async Task<IReadOnlyList<LineaDeLote>> Lotes(
        Guid almacenId,
        DateOnly? caducanAntesDe = null,
        CancellationToken cancelacion = default)
    {
        _ = await almacenes.PorId(almacenId, cancelacion)
            ?? throw new NoEncontrado("No existe el almacen.");

        var porId = (await articulos.Listar(incluirBajas: true, cancelacion))
            .ToDictionary(articulo => articulo.Id);

        return
        [
            .. (await valoracion.Lotes(almacenId, caducanAntesDe, cancelacion))
                // Solo los que se llevan por lotes. Los demas tambien tienen capas, pero
                // ahi una capa es un detalle de como se valora y no dice nada de lo que hay.
                .Where(capa => porId[capa.ArticuloId].LlevaLotes)
                .Select(capa => new LineaDeLote(
                    capa.ArticuloId,
                    porId[capa.ArticuloId].Referencia,
                    porId[capa.ArticuloId].Nombre,
                    capa.Lote,
                    capa.Caducidad,
                    capa.CantidadRestante,
                    capa.CosteRestante))
        ];
    }
}
