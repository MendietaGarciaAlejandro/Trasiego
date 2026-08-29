using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trasiego.Aplicacion.Documentos;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Contratos;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Api.Controladores;

[ApiController]
[Authorize]
[Route("api/documentos")]
public class DocumentosController(
    ServicioDeDocumentos documentos,
    ServicioDeMovimientos movimientos) : ControllerBase
{
    /// <summary>Los documentos de un almacen, de los mas recientes a los mas viejos.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<DocumentoVisto>> Listar(
        [FromQuery] Guid almacenId,
        [FromQuery] EstadoDeDocumento? estado,
        CancellationToken cancelacion) =>
        [.. (await documentos.DeAlmacen(almacenId, estado, cancelacion)).Select(DocumentoVisto.De)];

    [HttpGet("{id:guid}")]
    public async Task<DocumentoVisto> PorId(Guid id, CancellationToken cancelacion) =>
        DocumentoVisto.De(await documentos.PorId(id, cancelacion));

    /// <summary>Abre un documento en borrador. Todavia no mueve nada.</summary>
    [HttpPost]
    public async Task<ActionResult<DocumentoVisto>> Abrir(
        AbrirDocumento peticion,
        CancellationToken cancelacion)
    {
        var documento = await documentos.Abrir(
            peticion.Tipo, peticion.Numero, peticion.AlmacenId, peticion.FechaContable,
            peticion.AlmacenDestinoId, peticion.Concepto, cancelacion);

        return CreatedAtAction(
            nameof(PorId), new { id = documento.Id }, DocumentoVisto.De(documento));
    }

    [HttpPost("{id:guid}/lineas")]
    public async Task<DocumentoVisto> AgregarLinea(
        Guid id,
        LineaPedida peticion,
        CancellationToken cancelacion) =>
        DocumentoVisto.De(await documentos.AgregarLinea(
            id, peticion.ArticuloId, Cantidad.De(peticion.Cantidad),
            Importe.De(peticion.Coste), cancelacion));

    [HttpDelete("{id:guid}/lineas/{lineaId:guid}")]
    public async Task<DocumentoVisto> QuitarLinea(
        Guid id,
        Guid lineaId,
        CancellationToken cancelacion) =>
        DocumentoVisto.De(await documentos.QuitarLinea(id, lineaId, cancelacion));

    /// <summary>
    /// Lo da por bueno y genera sus movimientos, todos de una vez. A partir de aqui no se
    /// toca.
    /// </summary>
    [HttpPost("{id:guid}/registrar")]
    public async Task<IReadOnlyList<MovimientoVisto>> Registrar(
        Guid id,
        CancellationToken cancelacion) =>
        [.. (await movimientos.RegistrarDocumento(id, cancelacion)).Select(MovimientoVisto.De)];
}
