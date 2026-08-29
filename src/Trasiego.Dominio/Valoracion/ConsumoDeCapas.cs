using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Valoracion;

/// <summary>Lo que se saca de cada capa para cubrir una salida.</summary>
public readonly record struct TomaDeCapa(Guid CapaId, Cantidad Cantidad, Importe Coste);

public static class ConsumoDeCapas
{
    /// <summary>
    /// Vacia capas por orden de antiguedad hasta cubrir la cantidad pedida, y deja dicho
    /// cuanto salio de cada una y a que coste.
    /// </summary>
    /// <remarks>
    /// Esto vale igual para los dos metodos de valoracion, y no por casualidad: a precio
    /// medio solo hay una capa abierta, asi que recorrer capas por antiguedad se queda en
    /// recorrer una, y sacar una parte proporcional de ella es exactamente la media
    /// ponderada. Lo que cambia entre un metodo y otro es la entrada, no la salida.
    /// </remarks>
    public static IReadOnlyList<TomaDeCapa> Consumir(
        IEnumerable<CapaDeExistencias> capas,
        Cantidad cantidad)
    {
        // El orden lo pone la fecha contable, no la de registro: una entrada que se teclea
        // hoy con albaran de la semana pasada es mas antigua que otra tecleada ayer con
        // fecha de ayer, y en FIFO sale antes.
        var porAntiguedad = capas
            .Where(capa => !capa.Agotada)
            .OrderBy(capa => capa.FechaContable)
            .ThenBy(capa => capa.MomentoDeRegistro)
            .ThenBy(capa => capa.Id);

        var tomas = new List<TomaDeCapa>();
        var pendiente = cantidad;

        foreach (var capa in porAntiguedad)
        {
            if (pendiente.EsCero) break;

            var deEsta = pendiente <= capa.CantidadRestante ? pendiente : capa.CantidadRestante;
            tomas.Add(new TomaDeCapa(capa.Id, deEsta, capa.Consumir(deEsta)));
            pendiente -= deEsta;
        }

        // Si el saldo decia que habia bastante y las capas no llegan, lo que esta roto es la
        // invariante del almacen. Mejor parar aqui que registrar una salida a medio valorar.
        if (!pendiente.EsCero)
            throw new Conflicto(
                $"Las capas no cubren la salida: faltan {pendiente} sin poder valorar.");

        return tomas;
    }
}
