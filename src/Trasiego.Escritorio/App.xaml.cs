using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trasiego.Interfaz.Cliente;

namespace Trasiego.Escritorio;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs argumentos)
    {
        base.OnStartup(argumentos);

        var configuracion = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        var direccion = configuracion["Trasiego:Direccion"]
            ?? throw new InvalidOperationException("Falta 'Trasiego:Direccion' en appsettings.json.");

        var servicios = new ServiceCollection();
        servicios.AddWpfBlazorWebView();

        // El escritorio no habla con la base de datos: habla con la Api, igual que hablara la
        // web. Asi las pantallas no saben de donde salen los datos y valen para las dos.
        servicios.AddSingleton(new HttpClient { BaseAddress = new Uri(direccion) });
        servicios.AddScoped<ClienteDeTrasiego>();

        Resources.Add("servicios", servicios.BuildServiceProvider());
    }
}
