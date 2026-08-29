using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trasiego.Aplicacion.Informes;
using Trasiego.Contratos;

namespace Trasiego.Api.Controladores;

[ApiController]
[Authorize]
[Route("api/informes")]
public class InformesController(ServicioDeInformes informes) : ControllerBase
{
    /// <summary>
    /// Lo que valia un almacen un dia concreto, articulo a articulo. Sale de sumar los
    /// movimientos hasta esa fecha, sin reconstruir nada.
    /// </summary>
    [HttpGet("valoracion")]
    public async Task<ValoracionVista> Valoracion(
        [FromQuery] Guid almacenId,
        [FromQuery] DateOnly fecha,
        CancellationToken cancelacion)
    {
        var lineas = await informes.ValoracionA(almacenId, fecha, cancelacion);

        return new ValoracionVista(
            fecha,
            lineas.Sum(linea => linea.Valor.Visible),
            [.. lineas.Select(linea => new LineaDeValoracionVista(
                linea.ArticuloId, linea.Referencia, linea.Nombre,
                linea.Cantidad.Valor, linea.Valor.Visible))]);
    }
}
