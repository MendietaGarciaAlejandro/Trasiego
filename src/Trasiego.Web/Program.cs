using System.Globalization;
using System.Net.Http;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Trasiego.Interfaz;
using Trasiego.Interfaz.Cliente;

var constructor = WebAssemblyHostBuilder.CreateDefault(args);

constructor.RootComponents.Add<Enrutado>("#app");

// La Api es quien sirve esta pagina, asi que la direccion base es la suya y no hace falta
// configurarla ni abrir CORS.
constructor.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(constructor.HostEnvironment.BaseAddress),
});

constructor.Services.AddScoped<ClienteDeTrasiego>();

// En el navegador la cultura la pone quien mire la pagina, y aqui los numeros y las fechas
// tienen que leerse igual que en el escritorio: comas decimales, euros detras y dias antes
// que meses.
var espanol = new CultureInfo("es-ES");
CultureInfo.DefaultThreadCurrentCulture = espanol;
CultureInfo.DefaultThreadCurrentUICulture = espanol;

await constructor.Build().RunAsync();
