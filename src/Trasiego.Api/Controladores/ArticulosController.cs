using Microsoft.AspNetCore.Mvc;
using Trasiego.Api.Contratos;
using Trasiego.Aplicacion.Catalogo;

namespace Trasiego.Api.Controladores;

[ApiController]
[Route("api/articulos")]
public class ArticulosController(ServicioDeArticulos articulos) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ArticuloVisto>> Listar(
        [FromQuery] bool incluirBajas = false,
        CancellationToken cancelacion = default) =>
        [.. (await articulos.Listar(incluirBajas, cancelacion)).Select(ArticuloVisto.De)];

    [HttpGet("{id:guid}")]
    public async Task<ArticuloVisto> PorId(Guid id, CancellationToken cancelacion) =>
        ArticuloVisto.De(await articulos.PorId(id, cancelacion));

    [HttpPost]
    public async Task<ActionResult<ArticuloVisto>> Alta(
        AltaDeArticulo peticion,
        CancellationToken cancelacion)
    {
        var articulo = await articulos.Alta(
            peticion.Referencia, peticion.Nombre, peticion.Unidad, peticion.Metodo, cancelacion);

        return CreatedAtAction(
            nameof(PorId), new { id = articulo.Id }, ArticuloVisto.De(articulo));
    }

    [HttpPut("{id:guid}/metodo")]
    public async Task<NoContentResult> CambiarMetodo(
        Guid id,
        CambioDeMetodo peticion,
        CancellationToken cancelacion)
    {
        await articulos.CambiarMetodoDeValoracion(id, peticion.Metodo, cancelacion);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<NoContentResult> DarDeBaja(Guid id, CancellationToken cancelacion)
    {
        await articulos.DarDeBaja(id, cancelacion);
        return NoContent();
    }
}
