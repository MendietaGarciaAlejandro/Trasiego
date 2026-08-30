using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trasiego.Dominio.Acceso;
using Trasiego.Contratos;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Api.Controladores;

[ApiController]
[Authorize]
[Route("api/movimientos")]
public class MovimientosController(ServicioDeMovimientos movimientos) : ControllerBase
{
    /// <summary>El historico de un articulo en un almacen, en el orden en que cuenta.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<MovimientoVisto>> Historico(
        [FromQuery] Guid articuloId,
        [FromQuery] Guid almacenId,
        CancellationToken cancelacion) =>
        [.. (await movimientos.Historico(articuloId, almacenId, cancelacion))
            .Select(MovimientoVisto.De)];

    /// <summary>
    /// La ficha del articulo: cada movimiento con el saldo de cantidad y de valor que dejaba
    /// detras.
    /// </summary>
    [HttpGet("kardex")]
    public async Task<IReadOnlyList<LineaDeKardex>> Kardex(
        [FromQuery] Guid articuloId,
        [FromQuery] Guid almacenId,
        CancellationToken cancelacion) =>
        [.. (await movimientos.Kardex(articuloId, almacenId, cancelacion)).Select(linea =>
            new LineaDeKardex(
                linea.Movimiento.Id,
                linea.Movimiento.FechaContable,
                linea.Movimiento.Tipo,
                linea.Movimiento.Motivo,
                linea.Movimiento.Concepto,
                linea.Movimiento.Cantidad.Valor,
                linea.Movimiento.Coste.Visible,
                linea.Cantidad.Valor,
                linea.Valor.Visible,
                linea.Movimiento.Retroactivo,
                linea.Documento,
                linea.Usuario))];

    /// <summary>Lo que hay y lo que vale ahora mismo.</summary>
    [HttpGet("existencias")]
    public async Task<ExistenciasVistas> Existencias(
        [FromQuery] Guid articuloId,
        [FromQuery] Guid almacenId,
        CancellationToken cancelacion)
    {
        var (saldo, valor) = await movimientos.Existencias(articuloId, almacenId, cancelacion);
        return new ExistenciasVistas(articuloId, almacenId, saldo.Valor, valor.Visible);
    }

    /// <summary>
    /// Registra una entrada. El coste que se manda es el de la entrada entera, no el de cada
    /// unidad.
    /// </summary>
    [HttpPost("entradas")]
    public async Task<MovimientoVisto> Entrada(
        EntradaPedida peticion,
        CancellationToken cancelacion) =>
        MovimientoVisto.De(await movimientos.RegistrarEntrada(
            peticion.ArticuloId, peticion.AlmacenId,
            Cantidad.De(peticion.Cantidad), Importe.De(peticion.Coste),
            peticion.FechaContable, peticion.Concepto, cancelacion));

    /// <summary>Registra una salida. El coste no se manda: sale de las capas.</summary>
    [HttpPost("salidas")]
    public async Task<MovimientoVisto> Salida(
        SalidaPedida peticion,
        CancellationToken cancelacion) =>
        MovimientoVisto.De(await movimientos.RegistrarSalida(
            peticion.ArticuloId, peticion.AlmacenId, Cantidad.De(peticion.Cantidad),
            peticion.FechaContable, peticion.Concepto, cancelacion));

    /// <summary>Devuelve parte de una salida, al coste al que salio.</summary>
    [HttpPost("devoluciones")]
    public async Task<MovimientoVisto> Devolucion(
        DevolucionPedida peticion,
        CancellationToken cancelacion) =>
        MovimientoVisto.De(await movimientos.DevolverSalida(
            peticion.SalidaId, Cantidad.De(peticion.Cantidad),
            peticion.FechaContable, peticion.Concepto, cancelacion));

    /// <summary>
    /// Mueve mercancia de un almacen a otro. El coste no se manda: es el que sale del
    /// origen, y ese mismo entra en el destino.
    /// </summary>
    [HttpPost("traspasos")]
    public async Task<TraspasoVisto> Traspaso(
        TraspasoPedido peticion,
        CancellationToken cancelacion)
    {
        var traspaso = await movimientos.Traspasar(
            peticion.ArticuloId, peticion.OrigenId, peticion.DestinoId,
            Cantidad.De(peticion.Cantidad), peticion.FechaContable, peticion.Concepto,
            cancelacion);

        return new TraspasoVisto(
            MovimientoVisto.De(traspaso.Salida), MovimientoVisto.De(traspaso.Entrada));
    }

    /// <summary>
    /// Cuadra el sistema con un recuento. Devuelve 204 si ya cuadraba y no hizo falta mover
    /// nada.
    /// </summary>
    [HttpPost("recuentos")]
    [Authorize(Roles = Roles.Responsable)]
    public async Task<ActionResult<MovimientoVisto>> Recuento(
        RecuentoPedido peticion,
        CancellationToken cancelacion)
    {
        var ajuste = await movimientos.Regularizar(
            peticion.ArticuloId, peticion.AlmacenId, Cantidad.De(peticion.Contada),
            peticion.FechaContable, peticion.Concepto, cancelacion);

        return ajuste is null ? NoContent() : Ok(MovimientoVisto.De(ajuste));
    }
}
