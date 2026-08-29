using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Trasiego.Contratos;
using Trasiego.Infraestructura.Persistencia;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class RenovacionTests(BaseDeDatosDePruebas baseDeDatos) : IAsyncLifetime
{
    private const string Galleta = "trasiego_renovacion";

    private ApiDePruebas _api = null!;
    private HttpClient _cliente = null!;

    public Task InitializeAsync()
    {
        _api = new ApiDePruebas(baseDeDatos.CadenaDeConexion);

        // Sin manejar cookies solo: aqui hace falta poder guardarse una vieja y volver a
        // presentarla, que es justo lo que haria quien robara una.
        _cliente = _api.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task La_renovacion_se_deja_en_una_cookie_que_la_pagina_no_puede_leer()
    {
        var respuesta = await Entrar();

        var galleta = respuesta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(Galleta));

        Assert.Contains("httponly", galleta, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", galleta, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/acceso", galleta, StringComparison.OrdinalIgnoreCase);

        // Y no sale por el cuerpo: si saliera, daria igual que la cookie fuera inaccesible.
        Assert.DoesNotContain("enovacion", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Con_la_renovacion_se_vuelve_a_entrar_sin_teclear_nada()
    {
        var renovacion = Renovacion(await Entrar());

        var respuesta = await Renovar(renovacion);
        respuesta.EnsureSuccessStatusCode();

        var entrada = (await respuesta.Content.ReadFromJsonAsync<EntradaVista>(ApiTests.Json))!;
        Assert.False(string.IsNullOrWhiteSpace(entrada.Token));
        Assert.Equal("Encargada de almacen", entrada.Nombre);
    }

    [Fact]
    public async Task Cada_renovacion_gasta_la_anterior()
    {
        var primera = Renovacion(await Entrar());
        var segunda = Renovacion(await Renovar(primera));

        Assert.NotEqual(primera, segunda);

        // La primera ya se gasto.
        var conLaVieja = await Renovar(primera);
        Assert.Equal(HttpStatusCode.Unauthorized, conLaVieja.StatusCode);
    }

    [Fact]
    public async Task Si_reaparece_una_renovacion_gastada_se_tiran_todas()
    {
        // Alguien tiene una copia: o la nuestra o la suya, y no hay forma de saber cual.
        var primera = Renovacion(await Entrar());
        var segunda = Renovacion(await Renovar(primera));

        await Renovar(primera);

        // La que era buena tampoco vale ya: a entrar otra vez quien sepa la contraseña.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Renovar(segunda)).StatusCode);
    }

    [Fact]
    public async Task Sin_cookie_no_hay_nada_que_renovar()
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/acceso/renovar");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.SendAsync(peticion)).StatusCode);
    }

    [Fact]
    public async Task Al_salir_la_renovacion_deja_de_valer()
    {
        var renovacion = Renovacion(await Entrar());

        using var salir = new HttpRequestMessage(HttpMethod.Post, "/api/acceso/salir");
        salir.Headers.Add("Cookie", $"{Galleta}={renovacion}");
        await _cliente.SendAsync(salir);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Renovar(renovacion)).StatusCode);
    }

    private Task<HttpResponseMessage> Entrar() => _cliente.PostAsJsonAsync(
        "/api/acceso",
        new AccesoPedido("encargada@trasiego.test", SembradorDeDesarrollo.Contrasena),
        ApiTests.Json);

    private Task<HttpResponseMessage> Renovar(string renovacion)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/acceso/renovar");
        peticion.Headers.Add("Cookie", $"{Galleta}={renovacion}");

        return _cliente.SendAsync(peticion);
    }

    private static string Renovacion(HttpResponseMessage respuesta) => respuesta.Headers
        .GetValues("Set-Cookie")
        .Single(galleta => galleta.StartsWith(Galleta))
        .Split(';')[0]
        .Split('=', 2)[1];
}
