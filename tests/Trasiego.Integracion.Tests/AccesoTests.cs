using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Trasiego.Contratos;
using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Catalogo;
using Trasiego.Infraestructura.Persistencia;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class AccesoTests(BaseDeDatosDePruebas baseDeDatos) : IAsyncLifetime
{
    private ApiDePruebas _api = null!;

    public Task InitializeAsync()
    {
        _api = new ApiDePruebas(baseDeDatos.CadenaDeConexion);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task Sin_identificarse_no_se_ve_nada()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/api/articulos");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_salud_y_la_pagina_web_siguen_siendo_publicas()
    {
        using var cliente = _api.CreateClient();

        Assert.True((await cliente.GetAsync("/salud")).IsSuccessStatusCode);
        Assert.True((await cliente.GetAsync("/")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Con_la_contrasena_cambiada_no_se_entra_y_no_se_dice_por_que()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/acceso",
            new AccesoPedido("encargada@trasiego.test", "la que no es"),
            ApiTests.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("El correo o la contraseña no son correctos.", await Detalle(respuesta));
    }

    [Fact]
    public async Task Un_correo_que_no_existe_da_el_mismo_aviso_que_una_contrasena_mala()
    {
        using var cliente = _api.CreateClient();

        // Si el aviso fuera distinto, probando correos se sabria cuales estan dados de alta.
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/acceso",
            new AccesoPedido("nadie@trasiego.test", SembradorDeDesarrollo.Contrasena),
            ApiTests.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("El correo o la contraseña no son correctos.", await Detalle(respuesta));
    }

    [Fact]
    public async Task Un_operario_mueve_mercancia()
    {
        using var responsable = _api.CreateClient();
        await ApiTests.Identificarse(responsable, "encargada@trasiego.test");

        var (articulo, almacen) = await Catalogo(responsable);

        using var operario = _api.CreateClient();
        await ApiTests.Identificarse(operario, "operario@trasiego.test");

        var respuesta = await operario.PostAsJsonAsync(
            "/api/movimientos/entradas",
            new EntradaPedida(articulo.Id, almacen.Id, 5m, 10m, Ayer),
            ApiTests.Json);

        Assert.True(respuesta.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Un_operario_no_cuadra_inventarios_ni_cierra_periodos()
    {
        using var responsable = _api.CreateClient();
        await ApiTests.Identificarse(responsable, "encargada@trasiego.test");

        var (articulo, almacen) = await Catalogo(responsable);

        using var operario = _api.CreateClient();
        await ApiTests.Identificarse(operario, "operario@trasiego.test");

        var recuento = await operario.PostAsJsonAsync(
            "/api/movimientos/recuentos",
            new RecuentoPedido(articulo.Id, almacen.Id, 3m, Ayer),
            ApiTests.Json);

        var cierre = await operario.PostAsJsonAsync(
            "/api/cierres", new CierrePedido(almacen.Id, Ayer), ApiTests.Json);

        var alta = await operario.PostAsJsonAsync(
            "/api/articulos",
            new AltaDeArticulo("NO-DEBERIA", "Ni de broma", UnidadDeMedida.Unidad),
            ApiTests.Json);

        Assert.Equal(HttpStatusCode.Forbidden, recuento.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cierre.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, alta.StatusCode);
    }

    [Fact]
    public async Task El_responsable_si_cuadra_inventarios()
    {
        using var responsable = _api.CreateClient();
        await ApiTests.Identificarse(responsable, "encargada@trasiego.test");

        var (articulo, almacen) = await Catalogo(responsable);

        await responsable.PostAsJsonAsync(
            "/api/movimientos/entradas",
            new EntradaPedida(articulo.Id, almacen.Id, 5m, 10m, Ayer),
            ApiTests.Json);

        var recuento = await responsable.PostAsJsonAsync(
            "/api/movimientos/recuentos",
            new RecuentoPedido(articulo.Id, almacen.Id, 3m, Hoy),
            ApiTests.Json);

        Assert.True(recuento.IsSuccessStatusCode);
    }

    [Fact]
    public async Task No_se_dan_de_alta_dos_usuarios_con_el_mismo_correo()
    {
        using var cliente = _api.CreateClient();
        await ApiTests.Identificarse(cliente, "encargada@trasiego.test");

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/acceso/usuarios",
            new AltaDeUsuario(
                "ENCARGADA@trasiego.test", "Otra encargada", "loquesea", RolDeUsuario.Operario),
            ApiTests.Json);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    private static int _siguiente;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);
    private static DateOnly Ayer => Hoy.AddDays(-1);

    private static async Task<(ArticuloVisto Articulo, AlmacenVisto Almacen)> Catalogo(
        HttpClient responsable)
    {
        var numero = Interlocked.Increment(ref _siguiente);

        var articulo = await Leer<ArticuloVisto>(await responsable.PostAsJsonAsync(
            "/api/articulos",
            new AltaDeArticulo($"ACC-{numero}", $"Articulo {numero}", UnidadDeMedida.Unidad),
            ApiTests.Json));

        var almacen = await Leer<AlmacenVisto>(await responsable.PostAsJsonAsync(
            "/api/almacenes",
            new AltaDeAlmacen($"K{numero}", $"Almacen {numero}"),
            ApiTests.Json));

        return (articulo, almacen);
    }

    private static async Task<T> Leer<T>(HttpResponseMessage respuesta)
    {
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(ApiTests.Json))!;
    }

    private static async Task<string> Detalle(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return problema.RootElement.GetProperty("detail").GetString() ?? "";
    }
}
