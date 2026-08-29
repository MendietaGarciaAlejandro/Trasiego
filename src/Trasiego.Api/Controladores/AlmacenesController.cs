using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trasiego.Dominio.Acceso;
using Trasiego.Contratos;
using Trasiego.Aplicacion.Almacenes;

namespace Trasiego.Api.Controladores;

[ApiController]
[Authorize]
[Route("api/almacenes")]
public class AlmacenesController(ServicioDeAlmacenes almacenes) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<AlmacenVisto>> Listar(
        [FromQuery] bool incluirBajas = false,
        CancellationToken cancelacion = default) =>
        [.. (await almacenes.Listar(incluirBajas, cancelacion)).Select(AlmacenVisto.De)];

    [HttpGet("{id:guid}")]
    public async Task<AlmacenVisto> PorId(Guid id, CancellationToken cancelacion) =>
        AlmacenVisto.De(await almacenes.PorId(id, cancelacion));

    [HttpPost]
    [Authorize(Roles = Roles.Responsable)]
    public async Task<ActionResult<AlmacenVisto>> Alta(
        AltaDeAlmacen peticion,
        CancellationToken cancelacion)
    {
        var almacen = await almacenes.Alta(
            peticion.Codigo, peticion.Nombre, peticion.PermiteDescubierto, cancelacion);

        return CreatedAtAction(nameof(PorId), new { id = almacen.Id }, AlmacenVisto.De(almacen));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Responsable)]
    public async Task<NoContentResult> DarDeBaja(Guid id, CancellationToken cancelacion)
    {
        await almacenes.DarDeBaja(id, cancelacion);
        return NoContent();
    }
}
