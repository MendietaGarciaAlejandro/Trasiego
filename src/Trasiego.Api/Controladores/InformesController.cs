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

    /// <summary>
    /// Lo que hay en un almacen repartido por lotes, en el orden en que va a ir saliendo:
    /// primero lo que antes caduca. Con <c>caducanAntesDe</c>, solo lo que vence antes de
    /// esa fecha.
    /// </summary>
    [HttpGet("lotes")]
    public async Task<IReadOnlyList<LineaDeLoteVista>> Lotes(
        [FromQuery] Guid almacenId,
        [FromQuery] DateOnly? caducanAntesDe,
        CancellationToken cancelacion)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        return
        [
            .. (await informes.Lotes(almacenId, caducanAntesDe, cancelacion))
                .Select(linea => new LineaDeLoteVista(
                    linea.ArticuloId, linea.Referencia, linea.Nombre,
                    linea.Lote, linea.Caducidad,
                    linea.Cantidad.Valor, linea.Valor.Visible,
                    linea.Caducidad is { } cuando && cuando < hoy)),
        ];
    }
}
