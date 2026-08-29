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
    Importe Valor)
{
    public IReadOnlyList<CosteDeSalida> Descuadradas =>
        [.. Salidas.Where(salida => !salida.Cuadra)];
}

/// <summary>
/// Vuelve a valorar un historico desde cero, sin tocar nada, para poder decir en cuanto se
/// aparta la valoracion que hay de la que saldria si los movimientos hubieran llegado en
/// orden.
/// </summary>
/// <remarks>
/// Las piezas son las mismas que usa el servicio de movimientos: las mismas capas, el mismo
/// consumo por antiguedad, el mismo reparto de devoluciones. Lo unico que esta escrito dos
/// veces es el orquestado, y de que las dos versiones no se separen se encarga un test que
/// reproduce historicos sin retroactivos y exige que salga exactamente lo mismo.
/// </remarks>
public static class Recalculo
{
    public static Reproduccion Reproducir(
        MetodoDeValoracion metodo,
        IEnumerable<Movimiento> movimientos,
        Cantidad aperturaCantidad,
        Importe aperturaValor,
        DateOnly aperturaFecha)
    {
        var capas = new List<CapaDeExistencias>();
        var descubiertos = new List<Descubierto>();
        var consumosPorSalida = new Dictionary<Guid, List<ConsumoDeCapa>>();
        var salidas = new List<CosteDeSalida>();

        if (!aperturaCantidad.EsCero || !aperturaValor.EsCero)
            capas.Add(new CapaDeExistencias(
                Guid.Empty, Guid.Empty, Guid.Empty,
                aperturaCantidad, aperturaValor, aperturaFecha, DateTimeOffset.MinValue));

        var cantidad = Saldo.De(aperturaCantidad);
        var valor = aperturaValor;

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

        return new Reproduccion(salidas, cantidad, valor);

        void Entra(Movimiento entrada)
        {
            var coste = entrada.Motivo is MotivoDeMovimiento.Devolucion
                ? Devuelve(entrada)
                : entrada.Coste;

            // Una devolucion en FIFO ya ha repuesto sus capas; lo demas pasa por el
            // recorrido normal de tapar descubiertos y abrir o engordar capa.
            if (entrada.Motivo is MotivoDeMovimiento.Devolucion
                && metodo is MetodoDeValoracion.Fifo) return;

            Coloca(entrada, entrada.Cantidad, coste);
        }

        Importe Devuelve(Movimiento devolucion)
        {
            // Si la salida original queda por debajo del arranque no hay consumos que
            // deshacer, asi que se toma tal cual el coste con el que se registro.
            if (devolucion.MovimientoOrigenId is not { } origen
                || !consumosPorSalida.TryGetValue(origen, out var consumos))
                return devolucion.Coste;

            var vueltas = Devoluciones.Repartir(consumos, devolucion.Cantidad);

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

            foreach (var descubierto in descubiertos.Where(d => !d.Saldado))
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

            if (abierta is null)
                capas.Add(new CapaDeExistencias(
                    Guid.Empty, Guid.Empty, entrada.Id, queda, quedaCoste,
                    entrada.FechaContable, entrada.MomentoDeRegistro));
            else
                abierta.Absorber(queda, quedaCoste);
        }

        Importe Sale(Movimiento salida)
        {
            var disponible = capas.Aggregate(
                Cantidad.Cero, (suma, capa) => suma + capa.CantidadRestante);

            var deLasCapas = salida.Cantidad <= disponible ? salida.Cantidad : disponible;
            var tomas = ConsumoDeCapas.Consumir(capas, deLasCapas);

            consumosPorSalida[salida.Id] =
            [
                .. tomas.Select((toma, orden) =>
                    new ConsumoDeCapa(salida.Id, toma.CapaId, orden, toma.Cantidad, toma.Coste))
            ];

            var coste = tomas.Aggregate(Importe.Cero, (suma, toma) => suma + toma.Coste);
            var faltan = salida.Cantidad - deLasCapas;

            if (faltan.EsCero) return coste;

            var enDescubierto = Importe.De(UltimoUnitario() * faltan.Valor);
            descubiertos.Add(new Descubierto(
                Guid.Empty, Guid.Empty, salida.Id, faltan, enDescubierto));

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
    /// El orden en que cuentan los movimientos, que no es el orden en que se tecleraron: manda
    /// la fecha contable.
    /// </summary>
    private static IEnumerable<Movimiento> EnOrden(IEnumerable<Movimiento> movimientos) =>
        movimientos
            .OrderBy(movimiento => movimiento.FechaContable)
            .ThenBy(movimiento => movimiento.MomentoDeRegistro)
            .ThenBy(movimiento => movimiento.Id);
}
