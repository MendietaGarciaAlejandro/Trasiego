using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trasiego.Escritorio.Sesion;
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

        var direccion = new Uri(configuracion["Trasiego:Direccion"]
            ?? throw new InvalidOperationException("Falta 'Trasiego:Direccion' en appsettings.json."));

        // El contenedor de cookies se monta aqui aposta, en vez de dejar que lo ponga el
        // HttpClient por su cuenta: hace falta poder sacarle la renovacion para guardarla
        // entre una sesion y otra.
        var galletas = new CookieContainer();
        var cliente = new HttpClient(new HttpClientHandler { CookieContainer = galletas })
        {
            BaseAddress = direccion,
        };

        var servicios = new ServiceCollection();
        servicios.AddWpfBlazorWebView();

        // El escritorio no habla con la base de datos: habla con la Api, igual que hace la
        // web. Asi las pantallas no saben de donde salen los datos y valen para las dos.
        servicios.AddSingleton(cliente);

        // Unico para toda la aplicacion, no uno por pantalla: aqui solo hay una persona
        // delante, y ademas la memoria de sesion tiene que escuchar a ese mismo.
        servicios.AddSingleton<ClienteDeTrasiego>();

        var proveedor = servicios.BuildServiceProvider();

        var memoria = new MemoriaDeSesion(
            galletas, proveedor.GetRequiredService<ClienteDeTrasiego>(), direccion);

        memoria.Recordar();

        // Se guarda para que no se lo lleve el recolector: es quien esta escuchando los
        // cambios de sesion.
        Resources.Add("memoria", memoria);
        Resources.Add("servicios", proveedor);
    }
}
