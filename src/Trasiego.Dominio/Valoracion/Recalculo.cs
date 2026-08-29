using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Valoracion;

/// <summary>Lo que costo una salida y lo que habria costado reproduciendo el historico.</summary>
public record CosteDeSalida(Guid MovimientoId, Importe Registrado, Importe Reproducido)
{
    public bool Cuadra => Registrado == Reproducido;

    public Importe Diferencia => Reproducido - Registrado;
}

public record Reproduccion(
    IReadOnlyList<CosteDeSalida> Salidas,
    Saldo Cantidad,
    Importe Valor,
    IReadOnlyList<CapaDeExistencias> CapasNuevas,
    IReadOnlyList<ConsumoDeCapa> Consumos,
    IReadOnlyList<Descubierto> Descubiertos)
{
    public IReadOnlyList<CosteDeSalida> Descuadradas =>
        [.. Salidas.Where(salida => !salida.Cuadra)];
}

/// <summary>
/// Vuelve a valorar un historico desde el cierre, en el orden en que los movimientos
/// deberian haber llegado.
/// </summary>
/// <remarks>
/// <para>
/// No toca nada por su cuenta: recibe las capas de arranque y devuelve lo que habria que
/// guardar. Quien llama decide si solo quiere comparar o si ademas lo aplica.
/// </para>
/// <para>
/// Las piezas son las mismas que usa el servicio de movimientos: las mismas capas, el mismo
/// consumo por antiguedad, el mismo reparto de devoluciones. Lo unico que esta escrito dos
/// veces es el orquestado, y de que las dos versiones no se separen se encarga un test que
/// reproduce historicos sin retroactivos y exige que salga exactamente lo mismo.
/// </para>
/// </remarks>
public static class Recalculo
{
    public static Reproduccion Reproducir(
        MetodoDeValoracion metodo,
        Guid articuloId,
        Guid almacenId,
        IReadOnlyList<CapaDeExistencias> apertura,
        IEnumerable<Movimiento> movimientos)
    {
        var capas = new List<CapaDeExistencias>(apertura);
        var capasNuevas = new List<CapaDeExistencias>();
        var descubiertos = new List<Descubierto>();
        var consumos = new List<ConsumoDeCapa>();
        var consumosPorSalida = new Dictionary<Guid, List<ConsumoDeCapa>>();
        var salidas = new List<CosteDeSalida>();

        var cantidad = Saldo.De(
            apertura.Aggregate(Cantidad.Cero, (suma, capa) => suma + capa.CantidadRestante));
        var valor = apertura.Aggregate(Importe.Cero, (suma, capa) => suma + capa.CosteRestante);

        foreach (var movimiento in EnOrden(movimientos))
        {
            if (movimiento.Tipo is TipoDeMovimiento.Entrada)
            {
                Entra(movimiento);
                cantidad = Saldo.De(cantidad.Valor + movimiento.Cantidad.Valor);
                valor += movimiento.Coste;
            }
            else
            {
                var coste = Sale(movimiento);
                salidas.Add(new CosteDeSalida(movimiento.Id, movimiento.Coste, coste));

                cantidad = Saldo.De(cantidad.Valor - movimiento.Cantidad.Valor);
                valor -= coste;
            }
        }

        return new Reproduccion(salidas, cantidad, valor, capasNuevas, consumos, descubiertos);

        void Entra(Movimiento entrada)
        {
            var coste = entrada.Motivo is MotivoDeMovimiento.Devolucion
                ? Devuelve(entrada)
                : entrada.Coste;

            // Una devolucion en FIFO ya ha repuesto sus capas; lo demas pasa por el recorrido
            // normal de tapar descubiertos y abrir o engordar capa.
            if (entrada.Motivo is MotivoDeMovimiento.Devolucion
                && metodo is MetodoDeValoracion.Fifo) return;

            Coloca(entrada, entrada.Cantidad, coste);
        }

        Importe Devuelve(Movimiento devolucion)
        {
            // Una salida de un periodo cerrado no se puede devolver, asi que si la original
            // no esta en lo reproducido es que algo no cuadra; se respeta lo registrado.
            if (devolucion.MovimientoOrigenId is not { } origen
                || !consumosPorSalida.TryGetValue(origen, out var deLaSalida))
                return devolucion.Coste;

            var vueltas = Devoluciones.Repartir(deLaSalida, devolucion.Cantidad);

            if (metodo is MetodoDeValoracion.Fifo)
                foreach (var vuelta in vueltas)
                    capas.Single(capa => capa.Id == vuelta.CapaId)
                         .Reponer(vuelta.Cantidad, vuelta.Coste);

            return vueltas.Aggregate(Importe.Cero, (suma, vuelta) => suma + vuelta.Coste);
        }

        void Coloca(Movimiento entrada, Cantidad cantidadQueEntra, Importe costeQueEntra)
        {
            var queda = cantidadQueEntra;
            var quedaCoste = costeQueEntra;

            foreach (var descubierto in descubiertos.Where(pendiente => !pendiente.Saldado))
            {
                if (queda.EsCero) break;

                var tapa = queda <= descubierto.SinCubrir ? queda : descubierto.SinCubrir;
                quedaCoste -= descubierto.Cubrir(tapa);
                queda -= tapa;
            }

            if (queda.EsCero && quedaCoste.EsCero) return;

            var abierta = metodo is MetodoDeValoracion.PrecioMedio
                ? capas.FirstOrDefault(capa => !capa.Agotada)
                : null;

            if (abierta is not null)
            {
                abierta.Absorber(queda, quedaCoste);
                return;
            }

            var nueva = new CapaDeExistencias(
                articuloId, almacenId, entrada.Id, queda, quedaCoste,
                entrada.FechaContable, entrada.MomentoDeRegistro);

            capas.Add(nueva);
            capasNuevas.Add(nueva);
        }

        Importe Sale(Movimiento salida)
        {
            var disponible = capas.Aggregate(
                Cantidad.Cero, (suma, capa) => suma + capa.CantidadRestante);

            var deLasCapas = salida.Cantidad <= disponible ? salida.Cantidad : disponible;
            var tomas = ConsumoDeCapas.Consumir(capas, deLasCapas);

            var deEstaSalida = tomas
                .Select((toma, orden) =>
                    new ConsumoDeCapa(salida.Id, toma.CapaId, orden, toma.Cantidad, toma.Coste))
                .ToList();

            consumosPorSalida[salida.Id] = deEstaSalida;
            consumos.AddRange(deEstaSalida);

            var coste = tomas.Aggregate(Importe.Cero, (suma, toma) => suma + toma.Coste);
            var faltan = salida.Cantidad - deLasCapas;

            if (faltan.EsCero) return coste;

            var enDescubierto = Importe.De(UltimoUnitario() * faltan.Valor);
            descubiertos.Add(new Descubierto(
                articuloId, almacenId, salida.Id, faltan, enDescubierto));

            return coste + enDescubierto;
        }

        decimal UltimoUnitario() =>
            capas
                .Where(capa => !capa.CantidadInicial.EsCero)
                .OrderByDescending(capa => capa.FechaContable)
                .ThenByDescending(capa => capa.MomentoDeRegistro)
                .Select(capa => capa.CosteInicial.PorUnidad(capa.CantidadInicial))
                .FirstOrDefault();
    }

    /// <summary>
    /// El orden en que cuentan los movimientos, que no es en el que se tecleraron: manda la
    /// fecha contable.
    /// </summary>
    private static IEnumerable<Movimiento> EnOrden(IEnumerable<Movimiento> movimientos) =>
        movimientos
            .OrderBy(movimiento => movimiento.FechaContable)
            .ThenBy(movimiento => movimiento.MomentoDeRegistro)
            .ThenBy(movimiento => movimiento.Id);
}
