using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Trasiego.Contratos;

namespace Trasiego.Interfaz.Cliente;

/// <summary>Lo que la Api ha contestado que no se puede hacer, con su motivo.</summary>
public class FalloDeTrasiego(string mensaje) : Exception(mensaje);

public class ClienteDeTrasiego(HttpClient http)
{
    // Las mismas opciones que la Api: alli los enums salen por su nombre.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<IReadOnlyList<ArticuloVisto>> Articulos(bool incluirBajas = false) =>
        Traer<IReadOnlyList<ArticuloVisto>>($"api/articulos?incluirBajas={incluirBajas}");

    public Task<ArticuloVisto> AltaDeArticulo(AltaDeArticulo peticion) =>
        Mandar<AltaDeArticulo, ArticuloVisto>("api/articulos", peticion);

    public Task<IReadOnlyList<AlmacenVisto>> Almacenes(bool incluirBajas = false) =>
        Traer<IReadOnlyList<AlmacenVisto>>($"api/almacenes?incluirBajas={incluirBajas}");

    public Task<AlmacenVisto> AltaDeAlmacen(AltaDeAlmacen peticion) =>
        Mandar<AltaDeAlmacen, AlmacenVisto>("api/almacenes", peticion);

    public Task<IReadOnlyList<LineaDeKardex>> Kardex(Guid articuloId, Guid almacenId) =>
        Traer<IReadOnlyList<LineaDeKardex>>(
            $"api/movimientos/kardex?articuloId={articuloId}&almacenId={almacenId}");

    public Task<ExistenciasVistas> Existencias(Guid articuloId, Guid almacenId) =>
        Traer<ExistenciasVistas>(
            $"api/movimientos/existencias?articuloId={articuloId}&almacenId={almacenId}");

    public Task<MovimientoVisto> Entrada(EntradaPedida peticion) =>
        Mandar<EntradaPedida, MovimientoVisto>("api/movimientos/entradas", peticion);

    public Task<MovimientoVisto> Salida(SalidaPedida peticion) =>
        Mandar<SalidaPedida, MovimientoVisto>("api/movimientos/salidas", peticion);

    public Task<TraspasoVisto> Traspaso(TraspasoPedido peticion) =>
        Mandar<TraspasoPedido, TraspasoVisto>("api/movimientos/traspasos", peticion);

    public Task<ValoracionVista> Valoracion(Guid almacenId, DateOnly fecha) =>
        Traer<ValoracionVista>(
            $"api/informes/valoracion?almacenId={almacenId}&fecha={fecha:yyyy-MM-dd}");

    public Task<IReadOnlyList<CierreVisto>> Cierres(Guid almacenId) =>
        Traer<IReadOnlyList<CierreVisto>>($"api/cierres?almacenId={almacenId}");

    public Task<CierreVisto> Cerrar(CierrePedido peticion) =>
        Mandar<CierrePedido, CierreVisto>("api/cierres", peticion);

    public Task<IReadOnlyList<DescuadreVisto>> Comprobar(Guid cierreId) =>
        Traer<IReadOnlyList<DescuadreVisto>>($"api/cierres/{cierreId}/comprobacion");

    public Task<IReadOnlyList<Guid>> Sospechosos(Guid almacenId) =>
        Traer<IReadOnlyList<Guid>>($"api/recalculo/sospechosos?almacenId={almacenId}");

    public Task<ReproduccionVista> Comparar(Guid articuloId, Guid almacenId) =>
        Traer<ReproduccionVista>(
            $"api/recalculo/comparacion?articuloId={articuloId}&almacenId={almacenId}");

    public async Task<ReproduccionVista> Aplicar(Guid articuloId, Guid almacenId)
    {
        var respuesta = await http.PostAsync(
            $"api/recalculo?articuloId={articuloId}&almacenId={almacenId}", null);

        return await Leer<ReproduccionVista>(respuesta);
    }

    private async Task<T> Traer<T>(string ruta)
    {
        var respuesta = await http.GetAsync(ruta);
        return await Leer<T>(respuesta);
    }

    private async Task<TRespuesta> Mandar<TPeticion, TRespuesta>(string ruta, TPeticion peticion)
    {
        var respuesta = await http.PostAsJsonAsync(ruta, peticion, Json);
        return await Leer<TRespuesta>(respuesta);
    }

    private static async Task<T> Leer<T>(HttpResponseMessage respuesta)
    {
        if (!respuesta.IsSuccessStatusCode) throw new FalloDeTrasiego(await Motivo(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>
    /// El detalle del ProblemDetails, que es el mensaje que escribio quien puso la regla. La
    /// pantalla lo enseña tal cual: para eso se escribio pensando en quien lo iba a leer.
    /// </summary>
    private static async Task<string> Motivo(HttpResponseMessage respuesta)
    {
        try
        {
            using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            if (problema.RootElement.TryGetProperty("detail", out var detalle))
                return detalle.GetString() ?? "";
        }
        catch (JsonException)
        {
            // Si lo que ha vuelto no es un ProblemDetails, no hay motivo que enseñar.
        }

        return $"La aplicacion ha respondido {(int)respuesta.StatusCode}.";
    }
}
