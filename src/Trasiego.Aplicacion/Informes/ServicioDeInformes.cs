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

public class ServicioDeInformes(
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeArticulos articulos,
    IRepositorioDeMovimientos movimientos)
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
}
