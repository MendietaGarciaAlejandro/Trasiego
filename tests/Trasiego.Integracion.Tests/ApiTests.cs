using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Trasiego.Api.Contratos;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Valoracion;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class ApiTests(BaseDeDatosDePruebas baseDeDatos) : IAsyncLifetime
{
    // Las mismas opciones que la Api: alli los enums salen por su nombre, asi que cualquier
    // cliente tiene que leerlos igual.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static int _siguiente;

    private ApiDePruebas _api = null!;
    private HttpClient _cliente = null!;

    public Task InitializeAsync()
    {
        _api = new ApiDePruebas(baseDeDatos.CadenaDeConexion);
        _cliente = _api.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task Un_articulo_se_da_de_alta_y_se_recupera()
    {
        var alta = await Alta();

        var recuperado = await _cliente.GetFromJsonAsync<ArticuloVisto>(
            $"/api/articulos/{alta.Id}", Json);

        Assert.NotNull(recuperado);
        Assert.Equal(alta.Referencia, recuperado.Referencia);
        Assert.Equal(UnidadDeMedida.Unidad, recuperado.Unidad);
        Assert.True(recuperado.Activo);
    }

    [Fact]
    public async Task Los_enums_viajan_por_su_nombre_y_no_por_su_numero()
    {
        var alta = await Alta(metodo: MetodoDeValoracion.PrecioMedio);

        var json = await _cliente.GetStringAsync($"/api/articulos/{alta.Id}");

        Assert.Contains("\"PrecioMedio\"", json);
        Assert.Contains("\"Unidad\"", json);
    }

    [Fact]
    public async Task Pedir_un_articulo_que_no_existe_da_404_con_el_motivo()
    {
        var respuesta = await _cliente.GetAsync($"/api/articulos/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("No existe el articulo.", await Detalle(respuesta));
    }

    [Fact]
    public async Task Repetir_una_referencia_da_409()
    {
        var alta = await Alta();

        var respuesta = await _cliente.PostAsJsonAsync(
            "/api/articulos",
            new AltaDeArticulo(alta.Referencia, "Otro con la misma", UnidadDeMedida.Unidad), Json);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains(alta.Referencia, await Detalle(respuesta));
    }

    [Fact]
    public async Task Una_regla_de_almacen_incumplida_da_422_con_el_mensaje_entero()
    {
        var (articulo, almacen) = await Catalogo();

        await _cliente.PostAsJsonAsync("/api/movimientos/entradas", new EntradaPedida(
            articulo.Id, almacen.Id, 10m, 20m, Ayer), Json);

        var respuesta = await _cliente.PostAsJsonAsync("/api/movimientos/salidas",
            new SalidaPedida(articulo.Id, almacen.Id, 11m, Ayer), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Contains("quedan 10 ud", await Detalle(respuesta));
    }

    [Fact]
    public async Task Una_cantidad_negativa_es_una_peticion_mal_hecha_y_da_400()
    {
        var (articulo, almacen) = await Catalogo();

        var respuesta = await _cliente.PostAsJsonAsync("/api/movimientos/entradas",
            new EntradaPedida(articulo.Id, almacen.Id, -5m, 20m, Ayer), Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Entrada_salida_y_existencias_de_punta_a_punta()
    {
        var (articulo, almacen) = await Catalogo();

        await _cliente.PostAsJsonAsync("/api/movimientos/entradas", new EntradaPedida(
            articulo.Id, almacen.Id, 10m, 20m, Ayer, "compra inicial"), Json);

        var salida = await Leer<MovimientoVisto>(
            await _cliente.PostAsJsonAsync("/api/movimientos/salidas",
                new SalidaPedida(articulo.Id, almacen.Id, 4m, Hoy), Json));

        Assert.Equal(8m, salida.Coste);

        var existencias = await _cliente.GetFromJsonAsync<ExistenciasVistas>(
            $"/api/movimientos/existencias?articuloId={articulo.Id}&almacenId={almacen.Id}", Json);

        Assert.NotNull(existencias);
        Assert.Equal(6m, existencias.Saldo);
        Assert.Equal(12m, existencias.Valor);
    }

    [Fact]
    public async Task Un_recuento_que_cuadra_devuelve_204()
    {
        var (articulo, almacen) = await Catalogo();

        await _cliente.PostAsJsonAsync("/api/movimientos/entradas", new EntradaPedida(
            articulo.Id, almacen.Id, 10m, 20m, Ayer), Json);

        var respuesta = await _cliente.PostAsJsonAsync("/api/movimientos/recuentos",
            new RecuentoPedido(articulo.Id, almacen.Id, 10m, Hoy), Json);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);
    private static DateOnly Ayer => Hoy.AddDays(-1);

    private async Task<ArticuloVisto> Alta(MetodoDeValoracion metodo = MetodoDeValoracion.Fifo)
    {
        var numero = Interlocked.Increment(ref _siguiente);

        return await Leer<ArticuloVisto>(await _cliente.PostAsJsonAsync(
            "/api/articulos",
            new AltaDeArticulo($"API-{numero}", $"Articulo {numero}", UnidadDeMedida.Unidad, metodo), Json));
    }

    private async Task<(ArticuloVisto Articulo, AlmacenVisto Almacen)> Catalogo()
    {
        var articulo = await Alta();

        var almacen = await Leer<AlmacenVisto>(await _cliente.PostAsJsonAsync(
            "/api/almacenes",
            new AltaDeAlmacen($"W{_siguiente}", $"Almacen {_siguiente}"), Json));

        return (articulo, almacen);
    }

    private static async Task<T> Leer<T>(HttpResponseMessage respuesta)
    {
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string> Detalle(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return problema.RootElement.GetProperty("detail").GetString() ?? "";
    }
}
