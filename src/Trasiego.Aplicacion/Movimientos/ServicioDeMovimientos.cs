using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Movimientos;

/// <summary>
/// Un movimiento y como quedaba el almacen justo despues de el. El saldo corrido no se
/// guarda en ningun sitio: se saca recorriendo los movimientos en el orden en que cuentan,
/// que es la invariante leida de arriba abajo en vez de de golpe.
/// </summary>
public record LineaDeHistorico(
    Movimiento Movimiento,
    Saldo Cantidad,
    Importe Valor,
    string? Documento,
    string? Usuario);

/// <summary>Las dos mitades de un traspaso.</summary>
public record Traspaso(Movimiento Salida, Movimiento Entrada);

/// <summary>
/// Una salida y de que capas salio. Las tomas solo le hacen falta al traspaso, que tiene que
/// volver a abrir en el destino una capa por cada lote que vacio en el origen.
/// </summary>
internal record LoQueSale(Movimiento Movimiento, IReadOnlyList<TomaDeCapa> Tomas);

public class ServicioDeMovimientos(
    IRepositorioDeArticulos articulos,
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeMovimientos movimientos,
    IRepositorioDeValoracion valoracion,
    IRepositorioDeCierres cierres,
    IRepositorioDeDocumentos documentos,
    IRepositorioDeUsuarios usuarios,
    IUnidadDeTrabajo unidadDeTrabajo,
    IQuienRegistra quienRegistra,
    TimeProvider reloj)
{
    /// <summary>
    /// Registra una entrada. Si el almacen debia genero, lo primero que hace es taparlo.
    /// </summary>
    /// <param name="coste">
    /// Lo que ha costado la entrada entera, no lo que cuesta cada unidad. Se pide asi
    /// aposta: si se pidiera el precio unitario habria que multiplicarlo por la cantidad y
    /// el redondeo de esa multiplicacion ya no cuadraria con la factura.
    /// </param>
    public Task<Movimiento> RegistrarEntrada(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        string? concepto = null,
        string? lote = null,
        DateOnly? caducidad = null,
        CancellationToken cancelacion = default) =>
        unidadDeTrabajo.ConReintentos(
            cancela => Entrada(
                articuloId, almacenId, cantidad, coste, fechaContable, concepto,
                lote, caducidad, cancela),
            cancelacion);

    private async Task<Movimiento> Entrada(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        string? concepto,
        string? lote,
        DateOnly? caducidad,
        CancellationToken cancelacion)
    {
        var (articulo, almacen, retroactivo) = await Comprobaciones(
            articuloId, almacenId, cantidad, fechaContable, cancelacion);

        ComprobarLoQueEntra(articulo, lote, caducidad, fechaContable);

        var entrada = Meter(
            articulo, almacen, cantidad, coste, fechaContable, concepto,
            MotivoDeMovimiento.Ordinario, retroactivo);

        await MeterEnAlmacen(
            articulo, almacen, entrada, cantidad, coste, fechaContable, cancelacion,
            lote, caducidad);

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return entrada;
    }

    private static void ComprobarLoQueEntra(
        Articulo articulo,
        string? lote,
        DateOnly? caducidad,
        DateOnly fechaContable)
    {
        articulo.ComprobarLoteQueEntra(lote);

        if (caducidad is { } cuando && cuando < fechaContable)
            throw new ReglaDeNegocio(
                $"Ese lote caduco el {cuando:dd/MM/yyyy} y la entrada es del " +
                $"{fechaContable:dd/MM/yyyy}: entraria ya caducado.");

        if (caducidad is not null && !articulo.LlevaLotes)
            throw new ReglaDeNegocio(
                $"{articulo.Referencia} no se lleva por lotes: no hay donde apuntar una caducidad.");
    }

    /// <summary>
    /// Registra una salida. El coste no se teclea: sale de vaciar capas por antiguedad.
    /// </summary>
    /// <param name="lote">
    /// De que lote servir. Lo normal es no decirlo y que salga lo que antes caduque; se dice
    /// en una retirada de producto o cuando el cliente exige un lote concreto.
    /// </param>
    public Task<Movimiento> RegistrarSalida(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto = null,
        string? lote = null,
        CancellationToken cancelacion = default) =>
        unidadDeTrabajo.ConReintentos(
            cancela => Salida(
                articuloId, almacenId, cantidad, fechaContable, concepto, lote, cancela),
            cancelacion);

    private async Task<Movimiento> Salida(
        Guid articuloId,
        Guid almacenId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto,
        string? lote,
        CancellationToken cancelacion)
    {
        var (articulo, almacen, retroactivo) = await Comprobaciones(
            articuloId, almacenId, cantidad, fechaContable, cancelacion);

        var sale = await Sacar(
            articulo, almacen, cantidad, fechaContable, concepto,
            MotivoDeMovimiento.Ordinario, retroactivo, cancelacion, lote: lote);

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return sale.Movimiento;
    }

    /// <summary>
    /// Devuelve al almacen parte de una salida, al coste al que salio y no al de hoy.
    /// </summary>
    public Task<Movimiento> DevolverSalida(
        Guid salidaId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default) =>
        unidadDeTrabajo.ConReintentos(
            cancela => Devolucion(salidaId, cantidad, fechaContable, concepto, cancela),
            cancelacion);

    private async Task<Movimiento> Devolucion(
        Guid salidaId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto,
        CancellationToken cancelacion)
    {
        var salida = await movimientos.PorId(salidaId, cancelacion)
            ?? throw new NoEncontrado("No existe ese movimiento.");

        if (salida.Tipo is not TipoDeMovimiento.Salida)
            throw new ReglaDeNegocio("Solo se devuelve lo que ha salido.");

        // Una devolucion toca los consumos de la salida original. Si esa salida esta en un
        // periodo cerrado, tocarlos seria mover algo que se declaro cerrado, y ademas
        // dejaria el recalculo sin poder deshacerlo. Vuelve a entrar como entrada normal.
        var cerradoHasta = await cierres.Ultimo(salida.AlmacenId, cancelacion);
        if (cerradoHasta is not null && salida.FechaContable <= cerradoHasta.Hasta)
            throw new ReglaDeNegocio(
                $"Esa salida es del {salida.FechaContable:dd/MM/yyyy}, que ya esta cerrado. " +
                "Registra lo que vuelve como una entrada, con el coste que le corresponda.");

        var (articulo, almacen, retroactivo) = await Comprobaciones(
            salida.ArticuloId, salida.AlmacenId, cantidad, fechaContable, cancelacion);

        var consumos = await valoracion.ConsumosDe(salidaId, cancelacion);
        var vueltas = Devoluciones.Repartir(consumos, cantidad);
        var coste = vueltas.Aggregate(Importe.Cero, (suma, vuelta) => suma + vuelta.Coste);

        var devolucion = new Movimiento(
            articulo.Id, almacen.Id, TipoDeMovimiento.Entrada, cantidad, coste,
            fechaContable, reloj.GetUtcNow(), concepto,
            MotivoDeMovimiento.Devolucion, salidaId, retroactivo,
            usuarioId: quienRegistra.Id);

        movimientos.Agregar(devolucion);

        // El coste es el original con los dos criterios, pero no acaba en el mismo sitio. En
        // FIFO cada trozo vuelve a la capa de la que salio, que es lo que mantiene su coste
        // separado del de las demas. A precio medio no hay capas que distinguir: entra en la
        // que este abierta y rehace la media, que es lo que se espera de una media.
        if (articulo.Metodo is MetodoDeValoracion.PrecioMedio)
        {
            MeterEnCapas(
                articulo, almacen, devolucion, cantidad, coste, fechaContable,
                await valoracion.CapaAbierta(articulo.Id, almacen.Id, cancelacion));
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
    /// Mueve mercancia de un almacen a otro.
    /// </summary>
    /// <remarks>
    /// No es una salida y una entrada sueltas. El coste no lo teclea nadie: es el que sale
    /// del almacen de origen, y ese mismo entra en el de destino. Mover genero de sitio no
    /// puede cambiar lo que vale, y si el coste se tecleara aparte podria.
    /// </remarks>
    public Task<Traspaso> Traspasar(
        Guid articuloId,
        Guid origenId,
        Guid destinoId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto = null,
        string? lote = null,
        CancellationToken cancelacion = default) =>
        unidadDeTrabajo.ConReintentos(
            cancela => Mover(
                articuloId, origenId, destinoId, cantidad, fechaContable, concepto, lote, cancela),
            cancelacion);

    private async Task<Traspaso> Mover(
        Guid articuloId,
        Guid origenId,
        Guid destinoId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto,
        string? lote,
        CancellationToken cancelacion)
    {
        var traspaso = await MoverUna(
            articuloId, origenId, destinoId, cantidad, fechaContable, concepto, null, lote,
            cancelacion);

        // Un unico guardado para las dos mitades: si algo falla, no queda mercancia que ha
        // salido de un almacen y no ha llegado a ninguno.
        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return traspaso;
    }

    private async Task<Traspaso> MoverUna(
        Guid articuloId,
        Guid origenId,
        Guid destinoId,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto,
        Guid? documentoId,
        string? lote,
        CancellationToken cancelacion)
    {
        if (origenId == destinoId)
            throw new ReglaDeNegocio("El origen y el destino son el mismo almacen.");

        var (articulo, origen, saleTarde) = await Comprobaciones(
            articuloId, origenId, cantidad, fechaContable, cancelacion);

        var (_, destino, entraTarde) = await Comprobaciones(
            articuloId, destinoId, cantidad, fechaContable, cancelacion);

        var sale = await Sacar(
            articulo, origen, cantidad, fechaContable, concepto,
            MotivoDeMovimiento.Traspaso, saleTarde, cancelacion, documentoId, lote);

        var salida = sale.Movimiento;

        var entrada = new Movimiento(
            articulo.Id, destino.Id, TipoDeMovimiento.Entrada, cantidad, salida.Coste,
            fechaContable, reloj.GetUtcNow(), concepto,
            MotivoDeMovimiento.Traspaso, salida.Id, entraTarde, documentoId,
            quienRegistra.Id);

        movimientos.Agregar(entrada);

        if (articulo.LlevaLotes)
            // Una capa por lote de los que se vaciaron en el origen. Mover genero de sitio no
            // le cambia el lote igual que no le cambia lo que vale, asi que la mercancia
            // llega al destino repartida como salio y con sus mismas caducidades.
            foreach (var delLote in sale.Tomas.GroupBy(toma => (toma.Lote, toma.Caducidad)))
                valoracion.Agregar(new CapaDeExistencias(
                    articulo.Id, destino.Id, entrada.Id,
                    delLote.Aggregate(Cantidad.Cero, (suma, toma) => suma + toma.Cantidad),
                    delLote.Aggregate(Importe.Cero, (suma, toma) => suma + toma.Coste),
                    fechaContable, entrada.MomentoDeRegistro,
                    delLote.Key.Lote, delLote.Key.Caducidad));
        else
            await MeterEnAlmacen(
                articulo, destino, entrada, cantidad, salida.Coste, fechaContable, cancelacion);

        return new Traspaso(salida, entrada);
    }

    /// <summary>
    /// Convierte un documento en borrador en los movimientos que le corresponden.
    /// </summary>
    /// <remarks>
    /// Todo en un solo guardado. Un albaran de doce lineas o entra entero o no entra: no
    /// tiene sentido que la sexta falle y las cinco primeras se queden dentro, porque la
    /// mercancia llego junta.
    /// </remarks>
    public Task<IReadOnlyList<Movimiento>> RegistrarDocumento(
        Guid documentoId,
        CancellationToken cancelacion = default) =>
        unidadDeTrabajo.ConReintentos(cancela => Asentar(documentoId, cancela), cancelacion);

    private async Task<IReadOnlyList<Movimiento>> Asentar(
        Guid documentoId,
        CancellationToken cancelacion)
    {
        // Se relee en cada intento: si hubo que reintentar por un choque, lo que quedara
        // cargado ya no vale y el documento estaria marcado como registrado en memoria.
        var documento = await documentos.PorId(documentoId, cancelacion)
            ?? throw new NoEncontrado("No existe ese documento.");

        documento.DarPorRegistrado(reloj.GetUtcNow());

        var hechos = new List<Movimiento>();

        // En el orden del papel, que es el orden en que se valoran.
        foreach (var linea in documento.Lineas.OrderBy(linea => linea.Orden))
            hechos.AddRange(await Asentar(documento, linea, cancelacion));

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return hechos;
    }

    private async Task<IReadOnlyList<Movimiento>> Asentar(
        Documento documento,
        LineaDeDocumento linea,
        CancellationToken cancelacion)
    {
        if (documento.Tipo is TipoDeDocumento.Traspaso)
        {
            var traspaso = await MoverUna(
                linea.ArticuloId, documento.AlmacenId, documento.AlmacenDestinoId!.Value,
                linea.Cantidad, documento.FechaContable, documento.Concepto, documento.Id,
                linea.Lote, cancelacion);

            return [traspaso.Salida, traspaso.Entrada];
        }

        var (articulo, almacen, retroactivo) = await Comprobaciones(
            linea.ArticuloId, documento.AlmacenId, linea.Cantidad, documento.FechaContable,
            cancelacion);

        if (documento.Tipo is TipoDeDocumento.Entrega)
            return
            [
                (await Sacar(
                    articulo, almacen, linea.Cantidad, documento.FechaContable,
                    documento.Concepto, MotivoDeMovimiento.Ordinario, retroactivo, cancelacion,
                    documento.Id, linea.Lote)).Movimiento,
            ];

        ComprobarLoQueEntra(articulo, linea.Lote, linea.Caducidad, documento.FechaContable);

        var entrada = Meter(
            articulo, almacen, linea.Cantidad, linea.Coste, documento.FechaContable,
            documento.Concepto, MotivoDeMovimiento.Ordinario, retroactivo, documento.Id);

        await MeterEnAlmacen(
            articulo, almacen, entrada, linea.Cantidad, linea.Coste, documento.FechaContable,
            cancelacion, linea.Lote, linea.Caducidad);

        return [entrada];
    }

    /// <summary>
    /// Cuadra el sistema con lo que ha dado un recuento. Devuelve el movimiento que ha hecho
    /// falta, o nada si ya cuadraba.
    /// </summary>
    public Task<Movimiento?> Regularizar(
        Guid articuloId,
        Guid almacenId,
        Cantidad contada,
        DateOnly fechaContable,
        string? concepto = null,
        CancellationToken cancelacion = default) =>
        unidadDeTrabajo.ConReintentos(
            cancela => Ajuste(
                articuloId, almacenId, contada, fechaContable, concepto, cancela),
            cancelacion);

    private async Task<Movimiento?> Ajuste(
        Guid articuloId,
        Guid almacenId,
        Cantidad contada,
        DateOnly fechaContable,
        string? concepto,
        CancellationToken cancelacion)
    {
        var (articulo, almacen, retroactivo) = await Comprobaciones(
            articuloId, almacenId, contada, fechaContable, cancelacion, permitirCero: true);

        var saldo = await movimientos.SaldoDe(articulo.Id, almacen.Id, cancelacion: cancelacion);
        if (saldo == contada) return null;

        var diferencia = contada.Valor - saldo.Valor;

        var movimiento = diferencia < 0m
            ? (await Sacar(
                articulo, almacen, Cantidad.De(-diferencia), fechaContable, concepto,
                MotivoDeMovimiento.Regularizacion, retroactivo, cancelacion)).Movimiento
            : await MeterLoEncontrado(
                articulo, almacen, Cantidad.De(diferencia), saldo.Disponible,
                fechaContable, concepto, retroactivo, cancelacion);

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return movimiento;
    }

    /// <summary>El historico de un articulo en un almacen, en el orden en que cuenta.</summary>
    public Task<IReadOnlyList<Movimiento>> Historico(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        movimientos.Listar(articuloId, almacenId, null, false, cancelacion);

    /// <summary>
    /// La ficha del articulo en un almacen: cada movimiento con el saldo de cantidad y de
    /// valor que dejaba detras.
    /// </summary>
    public async Task<IReadOnlyList<LineaDeHistorico>> Kardex(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        var historico = await Historico(articuloId, almacenId, cancelacion);

        // Los numeros de los papeles de los que salieron, para poder enseñarlos: un albaran
        // se lee mejor que un identificador.
        var papeles = await documentos.NumerosDe(
            historico.Where(m => m.DocumentoId is not null).Select(m => m.DocumentoId!.Value),
            cancelacion);

        var quienes = await usuarios.NombresDe(
            historico.Where(m => m.UsuarioId is not null).Select(m => m.UsuarioId!.Value),
            cancelacion);

        var cantidad = 0m;
        var valor = Importe.Cero;
        var lineas = new List<LineaDeHistorico>();

        foreach (var movimiento in historico)
        {
            var entra = movimiento.Tipo is TipoDeMovimiento.Entrada;

            cantidad += entra ? movimiento.Cantidad.Valor : -movimiento.Cantidad.Valor;
            valor = entra ? valor + movimiento.Coste : valor - movimiento.Coste;

            var papel = movimiento.DocumentoId is { } id && papeles.TryGetValue(id, out var numero)
                ? numero
                : null;

            var quien = movimiento.UsuarioId is { } usuarioId
                     && quienes.TryGetValue(usuarioId, out var nombre)
                ? nombre
                : null;

            lineas.Add(new LineaDeHistorico(movimiento, Saldo.De(cantidad), valor, papel, quien));
        }

        return lineas;
    }

    /// <summary>Lo que hay y lo que vale.</summary>
    public async Task<(Saldo Saldo, Importe Valor)> Existencias(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        (await movimientos.SaldoDe(articuloId, almacenId, cancelacion: cancelacion),
         await valoracion.ValorDeLasExistencias(articuloId, almacenId, cancelacion));

    private Movimiento Meter(
        Articulo articulo,
        Almacen almacen,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        string? concepto,
        MotivoDeMovimiento motivo,
        bool retroactivo,
        Guid? documentoId = null)
    {
        var entrada = new Movimiento(
            articulo.Id, almacen.Id, TipoDeMovimiento.Entrada, cantidad, coste,
            fechaContable, reloj.GetUtcNow(), concepto, motivo, null, retroactivo, documentoId,
            quienRegistra.Id);

        movimientos.Agregar(entrada);
        return entrada;
    }

    /// <summary>
    /// Coloca lo que entra: primero tapa lo que el almacen debiera, y lo que sobre va a las
    /// capas.
    /// </summary>
    private async Task MeterEnAlmacen(
        Articulo articulo,
        Almacen almacen,
        Movimiento entrada,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        CancellationToken cancelacion,
        string? lote = null,
        DateOnly? caducidad = null)
    {
        // Un articulo con lotes no puede haber salido sin estar, asi que no hay agujero que
        // tapar: cada entrada abre su capa con su lote y su fecha.
        if (articulo.LlevaLotes)
        {
            valoracion.Agregar(new CapaDeExistencias(
                articulo.Id, almacen.Id, entrada.Id, cantidad, coste,
                fechaContable, entrada.MomentoDeRegistro, lote, caducidad));

            return;
        }

        var quedaCantidad = cantidad;
        var quedaCoste = coste;

        foreach (var descubierto in
                 await valoracion.DescubiertosPendientes(articulo.Id, almacen.Id, cancelacion))
        {
            if (quedaCantidad.EsCero) break;

            var tapa = quedaCantidad <= descubierto.SinCubrir
                ? quedaCantidad
                : descubierto.SinCubrir;

            quedaCoste -= descubierto.Cubrir(tapa);
            quedaCantidad -= tapa;
        }

        // Si no queda ni cantidad ni valor, la entrada se ha ido entera en tapar el agujero.
        // Puede quedar valor sin cantidad, y entonces si se abre capa: ver el comentario de
        // MeterEnCapas.
        if (quedaCantidad.EsCero && quedaCoste.EsCero) return;

        var abierta = articulo.Metodo is MetodoDeValoracion.PrecioMedio
            ? await valoracion.CapaAbierta(articulo.Id, almacen.Id, cancelacion)
            : null;

        MeterEnCapas(articulo, almacen, entrada, quedaCantidad, quedaCoste, fechaContable, abierta);
    }

    /// <summary>
    /// Aqui es donde se separan los dos criterios, y en ningun otro sitio: FIFO abre una capa
    /// por entrada para poder sacar cada una a su coste, y el precio medio las mete todas en
    /// la que ya estaba abierta, que es lo que hace la media.
    /// </summary>
    /// <remarks>
    /// La capa puede acabar con cantidad cero y valor distinto de cero cuando una entrada
    /// tapa justo un descubierto que se habia valorado por encima o por debajo de lo que
    /// costo de verdad. Esa diferencia es real y tiene que quedar contada en algun sitio,
    /// aunque no tenga existencias sobre las que apoyarse.
    /// </remarks>
    private void MeterEnCapas(
        Articulo articulo,
        Almacen almacen,
        Movimiento entrada,
        Cantidad cantidad,
        Importe coste,
        DateOnly fechaContable,
        CapaDeExistencias? abierta)
    {
        if (abierta is null)
            valoracion.Agregar(new CapaDeExistencias(
                articulo.Id, almacen.Id, entrada.Id, cantidad, coste,
                fechaContable, entrada.MomentoDeRegistro));
        else
            abierta.Absorber(cantidad, coste);
    }

    private async Task<LoQueSale> Sacar(
        Articulo articulo,
        Almacen almacen,
        Cantidad cantidad,
        DateOnly fechaContable,
        string? concepto,
        MotivoDeMovimiento motivo,
        bool retroactivo,
        CancellationToken cancelacion,
        Guid? documentoId = null,
        string? lote = null)
    {
        articulo.ComprobarLoteQueSale(lote);

        var todas = await valoracion.CapasConExistencias(articulo.Id, almacen.Id, cancelacion);

        // Con un lote pedido se sirve de ese y de ninguno mas. Es lo que hace falta para una
        // retirada, o para un cliente que exige el mismo lote que la vez anterior.
        var pedido = Comprobar.Lote(lote);
        var capas = pedido is null
            ? todas
            : [.. todas.Where(capa => capa.Lote == pedido)];

        if (pedido is not null && capas.Count == 0)
            throw new ReglaDeNegocio(
                $"En {almacen.Codigo} no queda nada del lote {pedido} de {articulo.Referencia}.");

        // Un recuento cuenta lo que hay, y lo caducado tambien esta ahi. Es ademas la unica
        // manera de darlo de baja: no se sirve, pero sigue ocupando sitio y valiendo dinero.
        var admiteCaducado = motivo is MotivoDeMovimiento.Regularizacion;

        var servibles = capas.Where(capa => admiteCaducado || !capa.CaducadaA(fechaContable));
        var disponible = servibles.Aggregate(
            Cantidad.Cero, (suma, capa) => suma + capa.CantidadRestante);

        if (cantidad > disponible && (articulo.LlevaLotes || !almacen.PermiteDescubierto))
            throw new ReglaDeNegocio(
                NoLlega(articulo, almacen, capas, cantidad, disponible, fechaContable, pedido));

        var deLasCapas = cantidad <= disponible ? cantidad : disponible;
        var tomas = ConsumoDeCapas.Consumir(capas, deLasCapas, fechaContable, admiteCaducado);
        var coste = tomas.Aggregate(Importe.Cero, (suma, toma) => suma + toma.Coste);

        var faltan = cantidad - deLasCapas;
        var costeEnDescubierto = faltan.EsCero
            ? Importe.Cero
            : await ValorarLoQueNoHay(articulo, almacen, faltan, cancelacion);

        var salida = new Movimiento(
            articulo.Id, almacen.Id, TipoDeMovimiento.Salida, cantidad, coste + costeEnDescubierto,
            fechaContable, reloj.GetUtcNow(), concepto, motivo, null, retroactivo, documentoId,
            quienRegistra.Id);

        movimientos.Agregar(salida);

        for (var orden = 0; orden < tomas.Count; orden++)
            valoracion.Agregar(new ConsumoDeCapa(
                salida.Id, tomas[orden].CapaId, orden, tomas[orden].Cantidad, tomas[orden].Coste));

        if (!faltan.EsCero)
            valoracion.Agregar(new Descubierto(
                articulo.Id, almacen.Id, salida.Id, faltan, costeEnDescubierto));

        return new LoQueSale(salida, tomas);
    }

    /// <summary>
    /// El motivo por el que no se puede servir, que no siempre es que no haya: puede haber de
    /// sobra y estar todo caducado, y eso hay que decirlo con esas palabras.
    /// </summary>
    private static string NoLlega(
        Articulo articulo,
        Almacen almacen,
        IReadOnlyList<CapaDeExistencias> capas,
        Cantidad cantidad,
        Cantidad disponible,
        DateOnly fechaContable,
        string? lote)
    {
        var unidad = articulo.Unidad.Abreviatura();

        var caducado = capas
            .Where(capa => capa.CaducadaA(fechaContable))
            .Aggregate(Cantidad.Cero, (suma, capa) => suma + capa.CantidadRestante);

        var deQue = lote is null
            ? $"{articulo.Referencia}"
            : $"{articulo.Referencia} del lote {lote}";

        var queja =
            $"No hay bastante {deQue} en {almacen.Codigo}: " +
            $"quedan {disponible} {unidad} y se piden {cantidad}.";

        if (!caducado.EsCero)
            queja += $" Hay {caducado} {unidad} mas, pero estan caducadas y no se sirven.";

        // Solo donde explica algo: si el almacen no admite descubierto de todas formas, decir
        // ademas que los lotes tampoco lo admiten es alargar la queja sin añadir nada.
        if (articulo.LlevaLotes && almacen.PermiteDescubierto)
            queja +=
                $" {almacen.Codigo} admite servir sin tener genero, pero un articulo con lotes " +
                "no sale sin estar: no habria lote del que sacarlo.";

        return queja;
    }

    private async Task<Importe> ValorarLoQueNoHay(
        Articulo articulo,
        Almacen almacen,
        Cantidad faltan,
        CancellationToken cancelacion)
    {
        // Lo que sale sin estar se valora al ultimo precio que se conoce, que es la mejor
        // suposicion disponible. Si resulta que la entrada que lo tapa costo otra cosa, la
        // diferencia la absorbe lo que quede en el almacen: lo ya valorado no se revisa.
        var unitario = await valoracion.UltimoCosteUnitario(articulo.Id, almacen.Id, cancelacion)
            ?? throw new ReglaDeNegocio(
                $"Por {almacen.Codigo} no ha pasado nunca {articulo.Referencia}: " +
                "no hay ningun precio con el que valorar lo que sale sin estar.");

        return Importe.De(unitario * faltan.Valor);
    }

    private async Task<Movimiento> MeterLoEncontrado(
        Articulo articulo,
        Almacen almacen,
        Cantidad diferencia,
        Cantidad hay,
        DateOnly fechaContable,
        string? concepto,
        bool retroactivo,
        CancellationToken cancelacion)
    {
        if (articulo.LlevaLotes)
            throw new ReglaDeNegocio(
                $"El recuento encuentra {diferencia} de mas de {articulo.Referencia}, que se " +
                "lleva por lotes: aqui no hay manera de saber de cual son. Registralas como " +
                "una entrada, diciendo el lote y lo que costaron.");

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
            MotivoDeMovimiento.Regularizacion, retroactivo);

        await MeterEnAlmacen(
            articulo, almacen, entrada, diferencia, coste, fechaContable, cancelacion);

        return entrada;
    }

    private async Task<(Articulo Articulo, Almacen Almacen, bool Retroactivo)> Comprobaciones(
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

        var cierre = await cierres.Ultimo(almacen.Id, cancelacion);
        if (cierre is not null && fechaContable <= cierre.Hasta)
            throw new ReglaDeNegocio(
                $"{almacen.Codigo} esta cerrado hasta el {cierre.Hasta:dd/MM/yyyy}, " +
                "esa fecha ya no admite movimientos.");

        // Llega tarde si hay algo registrado con fecha posterior. Se marca y ya esta: no se
        // revaloriza nada, pero queda constancia de que la valoracion de este articulo no es
        // la que saldria de recalcularlo desde cero.
        var ultima = await movimientos.UltimaFechaContable(articulo.Id, almacen.Id, cancelacion);
        var retroactivo = ultima is not null && fechaContable < ultima;

        return (articulo, almacen, retroactivo);
    }
}
