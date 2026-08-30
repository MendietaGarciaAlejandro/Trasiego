using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;

namespace Trasiego.Aplicacion.Catalogo;

public class ServicioDeArticulos(
    IRepositorioDeArticulos articulos,
    IRepositorioDeMovimientos movimientos)
{
    public Task<IReadOnlyList<Articulo>> Listar(
        bool incluirBajas = false,
        CancellationToken cancelacion = default) =>
        articulos.Listar(incluirBajas, cancelacion);

    public async Task<Articulo> PorId(Guid id, CancellationToken cancelacion = default) =>
        await articulos.PorId(id, cancelacion)
        ?? throw new NoEncontrado("No existe el articulo.");

    public async Task<Articulo> Alta(
        string referencia,
        string nombre,
        UnidadDeMedida unidad,
        MetodoDeValoracion metodo = MetodoDeValoracion.Fifo,
        bool llevaLotes = false,
        CancellationToken cancelacion = default)
    {
        var articulo = new Articulo(referencia, nombre, unidad, metodo, llevaLotes);

        // La unicidad de la referencia la impone la base de datos, pero comprobarla antes
        // permite decir de quien es en vez de soltar un choque de indice.
        if (await articulos.PorReferencia(articulo.Referencia, cancelacion) is not null)
            throw new Conflicto($"Ya hay un articulo con la referencia {articulo.Referencia}.");

        await articulos.Alta(articulo, cancelacion);
        return articulo;
    }

    public async Task DarDeBaja(Guid id, CancellationToken cancelacion = default)
    {
        var articulo = await PorId(id, cancelacion);
        articulo.DarDeBaja();

        await articulos.GuardarCambios(cancelacion);
    }

    public async Task CambiarMetodoDeValoracion(
        Guid articuloId,
        MetodoDeValoracion metodo,
        CancellationToken cancelacion = default)
    {
        var articulo = await PorId(articuloId, cancelacion);

        articulo.CambiarMetodo(metodo, await movimientos.TieneMovimientos(articuloId, cancelacion));

        await articulos.GuardarCambios(cancelacion);
    }
}
