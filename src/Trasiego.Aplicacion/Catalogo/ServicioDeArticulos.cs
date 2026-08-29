using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;

namespace Trasiego.Aplicacion.Catalogo;

public class ServicioDeArticulos(
    IRepositorioDeArticulos articulos,
    IRepositorioDeMovimientos movimientos)
{
    public async Task CambiarMetodoDeValoracion(
        Guid articuloId,
        MetodoDeValoracion metodo,
        CancellationToken cancelacion = default)
    {
        var articulo = await articulos.PorId(articuloId, cancelacion)
            ?? throw new NoEncontrado("No existe el articulo.");

        articulo.CambiarMetodo(metodo, await movimientos.TieneMovimientos(articuloId, cancelacion));

        await articulos.GuardarCambios(cancelacion);
    }
}
