using Microsoft.AspNetCore.Mvc;
using Trasiego.Api.Contratos;
using Trasiego.Aplicacion.Valoracion;

namespace Trasiego.Api.Controladores;

[ApiController]
[Route("api/recalculo")]
public class RecalculoController(ServicioDeRecalculo recalculo) : ControllerBase
{
    /// <summary>
    /// Los articulos de un almacen que conviene mirar: los que tienen algun movimiento que
    /// llego con fecha anterior a lo que ya estaba registrado.
    /// </summary>
    [HttpGet("sospechosos")]
    public Task<IReadOnlyList<Guid>> Sospechosos(
        [FromQuery] Guid almacenId,
        CancellationToken cancelacion) =>
        recalculo.ArticulosConRetroactivos(almacenId, cancelacion);

    /// <summary>Reproduce el historico y dice en cuanto se aparta. No cambia nada.</summary>
    [HttpGet("comparacion")]
    public async Task<ReproduccionVista> Comparar(
        [FromQuery] Guid articuloId,
        [FromQuery] Guid almacenId,
        CancellationToken cancelacion) =>
        ReproduccionVista.De(await recalculo.Comparar(articuloId, almacenId, cancelacion));

    /// <summary>
    /// Deshace lo que hay por encima del ultimo cierre y lo reconstruye en orden, corrigiendo
    /// las salidas que valorasen distinto.
    /// </summary>
    [HttpPost]
    public async Task<ReproduccionVista> Aplicar(
        [FromQuery] Guid articuloId,
        [FromQuery] Guid almacenId,
        CancellationToken cancelacion) =>
        ReproduccionVista.De(await recalculo.Aplicar(articuloId, almacenId, cancelacion));
}
