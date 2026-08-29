using Microsoft.AspNetCore.Mvc;
using Trasiego.Api.Contratos;
using Trasiego.Aplicacion.Cierres;

namespace Trasiego.Api.Controladores;

[ApiController]
[Route("api/cierres")]
public class CierresController(ServicioDeCierres cierres) : ControllerBase
{
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
        [.. (await cierres.Comprobar(id, cancelacion)).Select(DescuadreVisto.De)];
}
