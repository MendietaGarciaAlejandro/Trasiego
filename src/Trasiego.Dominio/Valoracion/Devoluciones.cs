using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Valoracion;

/// <summary>Lo que vuelve de un consumo concreto, con el coste que tuvo en su dia.</summary>
public readonly record struct VueltaACapa(Guid CapaId, Cantidad Cantidad, Importe Coste);

public static class Devoluciones
{
    /// <summary>
    /// Reparte lo que se devuelve entre los consumos de la salida, en el mismo orden en que
    /// se consumieron, y saca de cada uno el coste que tuvo entonces.
    /// </summary>
    public static IReadOnlyList<VueltaACapa> Repartir(
        IEnumerable<ConsumoDeCapa> consumos,
        Cantidad cantidad)
    {
        var vueltas = new List<VueltaACapa>();
        var pendiente = cantidad;

        foreach (var consumo in consumos)
        {
            if (pendiente.EsCero) break;
            if (consumo.SinDevolver.EsCero) continue;

            var deEste = pendiente <= consumo.SinDevolver ? pendiente : consumo.SinDevolver;
            vueltas.Add(new VueltaACapa(consumo.CapaId, deEste, consumo.Devolver(deEste)));
            pendiente -= deEste;
        }

        if (!pendiente.EsCero)
            throw new ReglaDeNegocio(
                $"De esa salida no quedan {cantidad} por devolver: sobran {pendiente}.");

        return vueltas;
    }
}
