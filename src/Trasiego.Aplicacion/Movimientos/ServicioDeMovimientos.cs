using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Movimientos;

public class ServicioDeMovimientos(
    IRepositorioDeArticulos articulos,
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeMovimientos movimientos,
    TimeProvider reloj)
{
    public Task<Movimiento> RegistrarEntrada(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default) =>
        Registrar(TipoDeMovimiento.Entrada, articuloId, almacenId, cantidad, fechaContable,
            concepto, cancelacion);

    public Task<Movimiento> RegistrarSalida(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default) =>
        Registrar(TipoDeMovimiento.Salida, articuloId, almacenId, cantidad, fechaContable,
            concepto, cancelacion);

    private async Task<Movimiento> Registrar(
        TipoDeMovimiento tipo,
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto,
        CancellationToken cancelacion)
    {
        var articulo = await articulos.PorId(articuloId, cancelacion)
            ?? throw new NoEncontrado("No existe el articulo.");

        if (!articulo.Activo)
            throw new ReglaDeNegocio($"El articulo {articulo.Referencia} esta de baja.");

        var almacen = await almacenes.PorId(almacenId, cancelacion)
            ?? throw new NoEncontrado("No existe el almacen.");

        if (!almacen.Activo)
            throw new ReglaDeNegocio($"El almacen {almacen.Codigo} esta de baja.");

        articulo.ComprobarCantidad(cantidad);

        // El dia contable se compara con el dia local del negocio, no con el UTC: en España
        // entre las 00:00 y las 02:00 el UTC todavia va por el dia anterior, y una entrada
        // tecleada a esa hora se rechazaria por futura sin serlo.
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (fechaContable > hoy)
            throw new ReglaDeNegocio("No se puede registrar un movimiento con fecha futura.");

        if (tipo is TipoDeMovimiento.Salida)
            await ComprobarQueHaySuficiente(articulo, almacen, cantidad, cancelacion);

        var movimiento = new Movimiento(
            articuloId, almacenId, tipo, cantidad, fechaContable, reloj.GetUtcNow(), concepto);

        await movimientos.Alta(movimiento, cancelacion);
        return movimiento;
    }

    private async Task ComprobarQueHaySuficiente(
        Articulo articulo,
        Almacen almacen,
        Cantidad cantidad,
        CancellationToken cancelacion)
    {
        // Se mira el saldo de hoy y no el de la fecha contable del movimiento: aunque el
        // albaran sea de la semana pasada, la mercancia sale del almacen ahora.
        var hay = await movimientos.Saldo(articulo.Id, almacen.Id, cancelacion: cancelacion);

        if (cantidad > hay)
            throw new ReglaDeNegocio(
                $"No hay bastante {articulo.Referencia} en {almacen.Codigo}: " +
                $"quedan {hay} {articulo.Unidad.Abreviatura()} y se piden {cantidad}.");
    }
}
