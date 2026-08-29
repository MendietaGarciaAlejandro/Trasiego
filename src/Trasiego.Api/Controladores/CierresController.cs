using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trasiego.Dominio.Acceso;
using Trasiego.Contratos;
using Trasiego.Api.Contratos;
using Trasiego.Aplicacion.Cierres;

namespace Trasiego.Api.Controladores;

[ApiController]
[Authorize(Roles = Roles.Responsable)]
[Route("api/cierres")]
public class CierresController(ServicioDeCierres cierres) : ControllerBase
{
    /// <summary>Los cierres de un almacen, del mas reciente al mas antiguo.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<CierreVisto>> Listar(
        [FromQuery] Guid almacenId,
        CancellationToken cancelacion) =>
        [.. (await cierres.DeAlmacen(almacenId, cancelacion)).Select(CierreVisto.De)];

    /// <summary>
    /// Cierra un almacen hasta un dia contable. A partir de ahi esa fecha no admite mas
    /// movimientos.
    /// </summary>
    [HttpPost]
    public async Task<CierreVisto> Cerrar(CierrePedido peticion, CancellationToken cancelacion) =>
        CierreVisto.De(await cierres.Cerrar(
            peticion.AlmacenId, peticion.Hasta, peticion.Concepto, cancelacion));

    /// <summary>
    /// Vuelve a sumar los movimientos hasta la fecha del cierre y lo compara con lo que se
    /// declaro entonces. Deberia salir vacio siempre.
    /// </summary>
    [HttpGet("{id:guid}/comprobacion")]
    public async Task<IReadOnlyList<DescuadreVisto>> Comprobar(
        Guid id,
        CancellationToken cancelacion) =>
        [.. (await cierres.Comprobar(id, cancelacion)).Select(Mapeos.Visto)];
}
