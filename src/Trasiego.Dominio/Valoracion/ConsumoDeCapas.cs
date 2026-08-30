using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Valoracion;

/// <summary>Lo que se saca de cada capa para cubrir una salida.</summary>
/// <remarks>
/// Lleva el lote de la capa aunque el consumo no lo necesite para nada: quien si lo necesita
/// es el traspaso, que tiene que abrir en el almacen de destino una capa por cada lote que
/// salio del de origen. Mover genero de sitio no le cambia el lote igual que no le cambia el
/// coste.
/// </remarks>
public readonly record struct TomaDeCapa(
    Guid CapaId,
    Cantidad Cantidad,
    Importe Coste,
    string? Lote = null,
    DateOnly? Caducidad = null);

public static class ConsumoDeCapas
{
    /// <summary>
    /// Vacia capas hasta cubrir la cantidad pedida, y deja dicho cuanto salio de cada una y
    /// a que coste.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Esto vale igual para los dos metodos de valoracion, y no por casualidad: a precio
    /// medio solo hay una capa abierta, asi que recorrer capas se queda en recorrer una, y
    /// sacar una parte proporcional de ella es exactamente la media ponderada. Lo que cambia
    /// entre un metodo y otro es la entrada, no la salida.
    /// </para>
    /// <para>
    /// Manda la caducidad y despues la antiguedad. Un articulo sin lotes no tiene ninguna
    /// caducidad, asi que ese primer criterio no desempata nada y queda el de siempre: FIFO
    /// es lo mismo de arriba con la mitad de la lista vacia.
    /// </para>
    /// </remarks>
    /// <param name="fecha">
    /// El dia contable del movimiento. Es a esa fecha a la que se mira si algo esta caducado,
    /// no a hoy: una salida con albaran de la semana pasada sale con las capas que valian
    /// entonces.
    /// </param>
    /// <param name="admiteCaducado">
    /// Solo lo pone una regularizacion. Lo caducado sigue estando y sigue valiendo dinero,
    /// pero no se sirve: se saca contandolo, que es como se da de baja una merma.
    /// </param>
    public static IReadOnlyList<TomaDeCapa> Consumir(
        IEnumerable<CapaDeExistencias> capas,
        Cantidad cantidad,
        DateOnly fecha,
        bool admiteCaducado = false)
    {
        // Primero lo que antes caduque, y lo que no caduca al final: no tiene sentido
        // guardar algo con fecha para sacar antes algo que no la tiene.
        //
        // El desempate lo pone la fecha contable y no la de registro: una entrada que se
        // teclea hoy con albaran de la semana pasada es mas antigua que otra tecleada ayer
        // con fecha de ayer, y sale antes.
        var porCaducidadYAntiguedad = capas
            .Where(capa => !capa.Agotada)
            .Where(capa => admiteCaducado || !capa.CaducadaA(fecha))
            .OrderBy(capa => capa.Caducidad ?? DateOnly.MaxValue)
            .ThenBy(capa => capa.FechaContable)
            .ThenBy(capa => capa.MomentoDeRegistro)
            .ThenBy(capa => capa.Id);

        var tomas = new List<TomaDeCapa>();
        var pendiente = cantidad;

        foreach (var capa in porCaducidadYAntiguedad)
        {
            if (pendiente.EsCero) break;

            var deEsta = pendiente <= capa.CantidadRestante ? pendiente : capa.CantidadRestante;
            tomas.Add(new TomaDeCapa(
                capa.Id, deEsta, capa.Consumir(deEsta), capa.Lote, capa.Caducidad));
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
