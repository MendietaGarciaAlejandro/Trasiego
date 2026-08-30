using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Cierres;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;

namespace Trasiego.Aplicacion.Valoracion;

/// <summary>
/// Como quedo el almacen que se pidio recalcular, y que otros hubo que rehacer detras porque
/// se alimentaban de sus traspasos.
/// </summary>
public record ResultadoDelRecalculo(
    Reproduccion Reproduccion,
    IReadOnlyList<Guid> OtrosAlmacenes);

public class ServicioDeRecalculo(
    IRepositorioDeArticulos articulos,
    IRepositorioDeMovimientos movimientos,
    IRepositorioDeValoracion valoracion,
    IRepositorioDeCierres cierres,
    IUnidadDeTrabajo unidadDeTrabajo)
{
    /// <summary>
    /// Tope de vueltas por si algo no se asienta. No deberia hacer falta: un traspaso siempre
    /// se alimenta de una salida anterior, asi que los costes van hacia delante en el tiempo
    /// y la cadena se acaba. Esta por si acaso, no por diseño.
    /// </summary>
    private const int TopeDeVueltas = 20;

    /// <summary>
    /// Reproduce el historico de un articulo desde el ultimo cierre y dice en que se aparta
    /// de lo que hay registrado. No cambia nada: solo mira.
    /// </summary>
    public async Task<Reproduccion> Comparar(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        var (articulo, cierre) = await Contexto(articuloId, almacenId, cancelacion);
        var fotos = await Fotos(cierre, articuloId, cancelacion);

        // Copias sueltas: comparar no debe poder tocar las capas de verdad ni por descuido.
        var apertura = fotos
            .Select(foto => new CapaDeExistencias(
                articuloId, almacenId, Guid.Empty, foto.Cantidad, foto.Coste,
                foto.FechaContable, foto.MomentoDeRegistro))
            .ToList();

        return Recalculo.Reproducir(
            articulo.Metodo, articuloId, almacenId, apertura,
            await movimientos.Listar(articuloId, almacenId, cierre?.Hasta, false, cancelacion));
    }

    /// <summary>
    /// Rehace la valoracion de un almacen y, si lo corregido salio hacia otro almacen en un
    /// traspaso, rehace tambien ese, y asi hasta que deja de moverse nada.
    /// </summary>
    public async Task<ResultadoDelRecalculo> Aplicar(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        var porHacer = new Queue<Guid>();
        porHacer.Enqueue(almacenId);

        var arrastrados = new List<Guid>();
        Reproduccion? elPedido = null;

        for (var vuelta = 0; porHacer.Count > 0; vuelta++)
        {
            if (vuelta >= TopeDeVueltas)
                throw new Conflicto(
                    "El recalculo no se asienta. Hay traspasos encadenados que se siguen " +
                    "moviendo vuelta tras vuelta, y eso hay que mirarlo a mano.");

            var almacen = porHacer.Dequeue();
            var reproduccion = await Rehacer(articuloId, almacen, cancelacion);

            if (almacen == almacenId) elPedido = reproduccion;
            else if (!arrastrados.Contains(almacen)) arrastrados.Add(almacen);

            foreach (var destino in await Propagar(reproduccion, cancelacion))
                if (!porHacer.Contains(destino)) porHacer.Enqueue(destino);
        }

        return new ResultadoDelRecalculo(elPedido!, arrastrados);
    }

    /// <summary>
    /// Los articulos de un almacen que conviene mirar: los que tienen algun movimiento que
    /// llego con fecha anterior a lo que ya estaba registrado.
    /// </summary>
    public Task<IReadOnlyList<Guid>> ArticulosConRetroactivos(
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        movimientos.ArticulosConRetroactivos(almacenId, cancelacion);

    /// <summary>
    /// Deshace todo lo que hay por encima del ultimo cierre de ese almacen y lo vuelve a
    /// construir en orden, corrigiendo lo que valorase distinto.
    /// </summary>
    private async Task<Reproduccion> Rehacer(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion)
    {
        var (articulo, cierre) = await Contexto(articuloId, almacenId, cancelacion);
        var fotos = await Fotos(cierre, articuloId, cancelacion);

        // Primero se tira lo de arriba: las capas que abrieron esos movimientos, lo que
        // consumieron y lo que dejaron a deber.
        await valoracion.Deshacer(
            articuloId, almacenId, cierre?.Hasta ?? DateOnly.MinValue, cancelacion);

        // Y las capas que venian de antes vuelven a como estaban el dia del cierre.
        var apertura = await valoracion.CapasPorId(
            fotos.Select(foto => foto.CapaId), cancelacion);

        foreach (var capa in apertura)
            capa.Restaurar(
                fotos.Single(foto => foto.CapaId == capa.Id).Cantidad,
                fotos.Single(foto => foto.CapaId == capa.Id).Coste);

        var enOrden = fotos
            .Select(foto => apertura.Single(capa => capa.Id == foto.CapaId))
            .ToList();

        var historico = await movimientos.Listar(
            articuloId, almacenId, cierre?.Hasta, true, cancelacion);

        var reproduccion = Recalculo.Reproducir(
            articulo.Metodo, articuloId, almacenId, enOrden, historico);

        foreach (var capa in reproduccion.CapasNuevas) valoracion.Agregar(capa);
        foreach (var consumo in reproduccion.Consumos) valoracion.Agregar(consumo);
        foreach (var descubierto in reproduccion.Descubiertos) valoracion.Agregar(descubierto);

        var porId = historico.ToDictionary(movimiento => movimiento.Id);
        foreach (var corregido in reproduccion.Descuadradas)
            porId[corregido.MovimientoId].CorregirCoste(corregido.Reproducido);

        await unidadDeTrabajo.GuardarCambios(cancelacion);
        return reproduccion;
    }

    /// <summary>
    /// Pone al dia las entradas de traspaso que se alimentaban de las salidas corregidas y
    /// devuelve los almacenes a los que fueron a parar, que son los que hay que rehacer.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> Propagar(
        Reproduccion reproduccion,
        CancellationToken cancelacion)
    {
        var corregidos = reproduccion.Descuadradas
            .ToDictionary(coste => coste.MovimientoId, coste => coste.Reproducido);

        if (corregidos.Count == 0) return [];

        var entradas = await movimientos.TraspasosAlimentadosPor(
            corregidos.Keys, conSeguimiento: true, cancelacion);

        if (entradas.Count == 0) return [];

        // Lo que sale de un almacen es lo que entra en el otro, tambien cuando lo que sale
        // resulta ser otra cosa de la que se creia.
        foreach (var entrada in entradas)
            entrada.CorregirCoste(corregidos[entrada.MovimientoOrigenId!.Value]);

        await unidadDeTrabajo.GuardarCambios(cancelacion);

        return [.. entradas.Select(entrada => entrada.AlmacenId).Distinct()];
    }

    private async Task<(Articulo Articulo, Cierre? Cierre)> Contexto(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion)
    {
        var articulo = await articulos.PorId(articuloId, cancelacion)
            ?? throw new NoEncontrado("No existe el articulo.");

        // Un articulo con lotes no se recalcula, y no por no poder: es que no hay nada que
        // recalcular.
        //
        // El recalculo existe porque sin lotes de que capa sale cada cosa no es un hecho sino
        // un convenio: diez tornillos de enero y diez de marzo son indistinguibles, y decimos
        // que salieron los de enero porque lo dice FIFO. Cuando aparece un albaran con fecha
        // anterior, el convenio cambia de opinion y hay que aplicarlo otra vez.
        //
        // Con lotes eso deja de ser un convenio: son cajas distintas y salio una concreta, y
        // quedo apuntado cuando paso. Un albaran que llega tarde no cambia de que caja salio
        // lo que ya esta en casa del cliente. Ademas, como un articulo con lotes no admite
        // descubierto, ninguna salida puede estar esperando a una entrada posterior que la
        // revalorice: los costes registrados son los definitivos.
        if (articulo.LlevaLotes)
            throw new ReglaDeNegocio(
                $"{articulo.Referencia} se lleva por lotes: de que lote salio cada cosa no es " +
                "una suposicion que rehacer, es lo que paso y esta apuntado. Aqui no hay nada " +
                "que recalcular.");

        return (articulo, await cierres.Ultimo(almacenId, cancelacion));
    }

    private async Task<IReadOnlyList<FotoDeCapa>> Fotos(
        Cierre? cierre,
        Guid articuloId,
        CancellationToken cancelacion) =>
        cierre is null ? [] : await cierres.FotosDe(cierre.Id, articuloId, cancelacion);
}
