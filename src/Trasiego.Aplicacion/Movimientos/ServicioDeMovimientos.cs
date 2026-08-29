using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Movimientos;

public class ServicioDeMovimientos(
    IRepositorioDeArticulos articulos,
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeMovimientos movimientos,
    IRepositorioDeValoracion valoracion,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    /// <summary>
    /// Registra una entrada y abre la capa que las salidas iran vaciando.
    /// </summary>
    /// <param name="coste">
    /// Lo que ha costado la entrada entera, no lo que cuesta cada unidad. Se pide asi
    /// aposta: si se pidiera el precio unitario habria que multiplicarlo por la cantidad y
    /// el redondeo de esa multiplicacion ya no cuadraria con la factura.
    /// </param>
    public async Task<Movimiento> RegistrarEntrada(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default)
    {
        var (articulo, almacen) = await Comprobaciones(
            articuloId, almacenId, cantidad, fechaContable, cancelacion);

        var entrada = Meter(
            articulo, almacen, cantidad, coste, fechaContable, concepto,
            MotivoDeMovimiento.Ordinario);

        await MeterEnCapas(articulo, almacen, entrada, cantidad, coste, fechaContable, cancelacion);

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return entrada;
    }

    /// <summary>
    /// Registra una salida. El coste no se teclea: sale de vaciar capas por antiguedad.
    /// </summary>
    public async Task<Movimiento> RegistrarSalida(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default)
    {
        var (articulo, almacen) = await Comprobaciones(
            articuloId, almacenId, cantidad, fechaContable, cancelacion);

        var salida = await Sacar(
            articulo, almacen, cantidad, fechaContable, concepto,
            MotivoDeMovimiento.Ordinario, cancelacion);

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return salida;
    }

    /// <summary>
    /// Devuelve al almacen parte de una salida, al coste al que salio y no al de hoy.
    /// </summary>
    public async Task<Movimiento> DevolverSalida(
        Guid salidaId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default)
    {
        var salida = await movimientos.PorId(salidaId, cancelacion)
            ?? throw new NoEncontrado("No existe ese movimiento.");

        if (salida.Tipo is not TipoDeMovimiento.Salida)
            throw new ReglaDeNegocio("Solo se devuelve lo que ha salido.");

        var (articulo, almacen) = await Comprobaciones(
            salida.ArticuloId, salida.AlmacenId, cantidad, fechaContable, cancelacion);

        var consumos = await valoracion.ConsumosDe(salidaId, cancelacion);
        var vueltas = Devoluciones.Repartir(consumos, cantidad);
        var coste = vueltas.Aggregate(Importe.Cero, (suma, vuelta) => suma + vuelta.Coste);

        var devolucion = new Movimiento(
            articulo.Id, almacen.Id, TipoDeMovimiento.Entrada, cantidad, coste,
            fechaContable, reloj.GetUtcNow(), concepto,
            MotivoDeMovimiento.Devolucion, salidaId);

        movimientos.Agregar(devolucion);

        // El coste es el original con los dos criterios, pero no acaba en el mismo sitio. En
        // FIFO cada trozo vuelve a la capa de la que salio, que es lo que mantiene su coste
        // separado del de las demas. A precio medio no hay capas que distinguir: entra en la
        // que este abierta y rehace la media, que es lo que se espera de una media.
        if (articulo.Metodo is MetodoDeValoracion.PrecioMedio)
        {
            await MeterEnCapas(
                articulo, almacen, devolucion, cantidad, coste, fechaContable, cancelacion);
        }
        else
        {
            var capas = await valoracion.CapasPorId(
                vueltas.Select(vuelta => vuelta.CapaId).Distinct(), cancelacion);

            foreach (var vuelta in vueltas)
                capas.Single(capa => capa.Id == vuelta.CapaId)
                     .Reponer(vuelta.Cantidad, vuelta.Coste);
        }

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return devolucion;
    }

    /// <summary>
    /// Cuadra el sistema con lo que ha dado un recuento. Devuelve el movimiento que ha hecho
    /// falta, o nada si ya cuadraba.
    /// </summary>
    public async Task<Movimiento?> Regularizar(
        Guid articuloId,
        Guid almacenId,
        Cantidad contada,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default)
    {
        var (articulo, almacen) = await Comprobaciones(
            articuloId, almacenId, contada, fechaContable, cancelacion, permitirCero: true);

        var hay = await movimientos.Saldo(articulo.Id, almacen.Id, cancelacion: cancelacion);
        if (contada == hay) return null;

        var movimiento = contada < hay
            ? await Sacar(
                articulo, almacen, hay - contada, fechaContable, concepto,
                MotivoDeMovimiento.Regularizacion, cancelacion)
            : await MeterLoEncontrado(
                articulo, almacen, contada - hay, hay, fechaContable, concepto, cancelacion);

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return movimiento;
    }

    private Movimiento Meter(
        Articulo articulo,
        Almacen almacen,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        string? concepto,
        MotivoDeMovimiento motivo)
    {
        var entrada = new Movimiento(
            articulo.Id, almacen.Id, TipoDeMovimiento.Entrada, cantidad, coste,
            fechaContable, reloj.GetUtcNow(), concepto, motivo);

        movimientos.Agregar(entrada);
        return entrada;
    }

    /// <summary>
    /// Mete la cantidad en las capas. Aqui es donde se separan los dos criterios, y en
    /// ningun otro sitio: FIFO abre una capa por entrada para poder sacar cada una a su
    /// coste, y el precio medio las mete todas en la que ya estaba abierta, que es lo que
    /// hace la media.
    /// </summary>
    private async Task MeterEnCapas(
        Articulo articulo,
        Almacen almacen,
        Movimiento entrada,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        CancellationToken cancelacion)
    {
        var abierta = articulo.Metodo is MetodoDeValoracion.PrecioMedio
            ? await valoracion.CapaAbierta(articulo.Id, almacen.Id, cancelacion)
            : null;

        if (abierta is null)
            valoracion.Agregar(new CapaDeExistencias(
                articulo.Id, almacen.Id, entrada.Id, cantidad, coste,
                fechaContable, entrada.MomentoDeRegistro));
        else
            abierta.Absorber(cantidad, coste);
    }

    private async Task<Movimiento> Sacar(
        Articulo articulo,
        Almacen almacen,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto,
        MotivoDeMovimiento motivo,
        CancellationToken cancelacion)
    {
        // Se mira el saldo de hoy y no el de la fecha contable del movimiento: aunque el
        // albaran sea de la semana pasada, la mercancia sale del almacen ahora.
        var hay = await movimientos.Saldo(articulo.Id, almacen.Id, cancelacion: cancelacion);
        if (cantidad > hay)
            throw new ReglaDeNegocio(
                $"No hay bastante {articulo.Referencia} en {almacen.Codigo}: " +
                $"quedan {hay} {articulo.Unidad.Abreviatura()} y se piden {cantidad}.");

        var capas = await valoracion.CapasConExistencias(articulo.Id, almacen.Id, cancelacion);
        var tomas = ConsumoDeCapas.Consumir(capas, cantidad);
        var coste = tomas.Aggregate(Importe.Cero, (suma, toma) => suma + toma.Coste);

        var salida = new Movimiento(
            articulo.Id, almacen.Id, TipoDeMovimiento.Salida, cantidad, coste,
            fechaContable, reloj.GetUtcNow(), concepto, motivo);

        movimientos.Agregar(salida);

        foreach (var toma in tomas)
            valoracion.Agregar(new ConsumoDeCapa(salida.Id, toma.CapaId, toma.Cantidad, toma.Coste));

        return salida;
    }

    private async Task<Movimiento> MeterLoEncontrado(
        Articulo articulo,
        Almacen almacen,
        Cantidad diferencia,
        Cantidad hay,
        DateOnly fechaContable,
        string? concepto,
        CancellationToken cancelacion)
    {
        if (hay.EsCero)
            throw new ReglaDeNegocio(
                $"No hay existencias de {articulo.Referencia} de las que sacar un coste. " +
                "Registra la diferencia como una entrada normal, con lo que costo.");

        // El unico sitio del proyecto donde se usa un coste unitario para calcular algo: lo
        // que aparece de mas vale lo mismo que lo que ya estaba, asi que entra al precio de
        // las existencias y el valor unitario del almacen no se mueve.
        var valor = await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id, cancelacion);
        var coste = Importe.De(valor.PorUnidad(hay) * diferencia.Valor);

        var entrada = Meter(
            articulo, almacen, diferencia, coste, fechaContable, concepto,
            MotivoDeMovimiento.Regularizacion);

        await MeterEnCapas(
            articulo, almacen, entrada, diferencia, coste, fechaContable, cancelacion);

        return entrada;
    }

    private async Task<(Articulo, Almacen)> Comprobaciones(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        CancellationToken cancelacion,
        bool permitirCero = false)
    {
        var articulo = await articulos.PorId(articuloId, cancelacion)
            ?? throw new NoEncontrado("No existe el articulo.");

        if (!articulo.Activo)
            throw new ReglaDeNegocio($"El articulo {articulo.Referencia} esta de baja.");

        var almacen = await almacenes.PorId(almacenId, cancelacion)
            ?? throw new NoEncontrado("No existe el almacen.");

        if (!almacen.Activo)
            throw new ReglaDeNegocio($"El almacen {almacen.Codigo} esta de baja.");

        // Un recuento puede dar cero, y eso es un dato. Lo que no vale es un movimiento de
        // cero, y de eso ya se encarga el propio Movimiento.
        if (!permitirCero || !cantidad.EsCero) articulo.ComprobarCantidad(cantidad);

        // El dia contable se compara con el dia local del negocio, no con el UTC: en Espana
        // entre las 00:00 y las 02:00 el UTC todavia va por el dia anterior, y una entrada
        // tecleada a esa hora se rechazaria por futura sin serlo.
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (fechaContable > hoy)
            throw new ReglaDeNegocio("No se puede registrar un movimiento con fecha futura.");

        return (articulo, almacen);
    }
}
