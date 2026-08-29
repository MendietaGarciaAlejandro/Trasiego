using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Trasiego.Integracion.Tests;

/// <summary>
/// Levanta la Api de verdad contra la base de datos de las pruebas. Lo que interesa probar
/// por aqui no son las reglas, que ya tienen sus tests, sino el borde: que cada fallo salga
/// con el codigo que le toca y con el mensaje entero.
/// </summary>
public sealed class ApiDePruebas(string cadenaDeConexion) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder constructor)
    {
        constructor.UseEnvironment("Development");

        constructor.ConfigureHostConfiguration(configuracion =>
            configuracion.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Trasiego"] = cadenaDeConexion,

                    // La clave de firma se pone aqui para que las pruebas no dependan de los
                    // secretos que tenga cada uno en su maquina.
                    ["Jwt:Clave"] = "clave-de-pruebas-lo-bastante-larga-para-hmac-sha256",
                }));

        return base.CreateHost(constructor);
    }
}
