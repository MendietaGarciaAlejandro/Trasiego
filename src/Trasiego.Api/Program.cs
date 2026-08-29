using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Infraestructura;
using Trasiego.Infraestructura.Persistencia;

var constructor = WebApplication.CreateBuilder(args);

var cadenaDeConexion = constructor.Configuration.GetConnectionString("Trasiego")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexion 'Trasiego'. En desarrollo esta en appsettings.Development.json.");

constructor.Services.AgregarInfraestructura(cadenaDeConexion);
constructor.Services.AddOpenApi();

constructor.Services.AddScoped<ServicioDeMovimientos>();
constructor.Services.AddSingleton(TimeProvider.System);

var app = constructor.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // En desarrollo se migra al arrancar para no tener que acordarse de hacerlo a mano.
    // En produccion las migraciones se aplican en el despliegue, no desde la aplicacion.
    using var ambito = app.Services.CreateScope();
    await ambito.ServiceProvider.GetRequiredService<ContextoDeTrasiego>().Database.MigrateAsync();
}

app.MapGet("/salud", async (ContextoDeTrasiego contexto) =>
    await contexto.Database.CanConnectAsync()
        ? Results.Ok(new { estado = "vivo" })
        : Results.Problem("No se llega a la base de datos.", statusCode: 503));

app.Run();
