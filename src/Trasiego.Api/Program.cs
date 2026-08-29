using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Trasiego.Api.Contratos;
using Trasiego.Api.Errores;
using Trasiego.Aplicacion.Almacenes;
using Trasiego.Aplicacion.Catalogo;
using Trasiego.Aplicacion.Cierres;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Aplicacion.Valoracion;
using Trasiego.Infraestructura;
using Trasiego.Infraestructura.Persistencia;

var constructor = WebApplication.CreateBuilder(args);

var cadenaDeConexion = constructor.Configuration.GetConnectionString("Trasiego")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexion 'Trasiego'. En desarrollo esta en appsettings.Development.json.");

constructor.Services.AgregarInfraestructura(cadenaDeConexion);

constructor.Services
    .AddControllers()
    .AddJsonOptions(opciones =>
        // Los enums viajan por su nombre. Un "Fifo" se entiende leyendo la respuesta; un 1 no,
        // y ademas ata al cliente al orden en que estan declarados.
        opciones.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

constructor.Services.AddOpenApi();

constructor.Services.AddScoped<ServicioDeArticulos>();
constructor.Services.AddScoped<ServicioDeAlmacenes>();
constructor.Services.AddScoped<ServicioDeMovimientos>();
constructor.Services.AddScoped<ServicioDeCierres>();
constructor.Services.AddScoped<ServicioDeRecalculo>();
constructor.Services.AddSingleton(TimeProvider.System);

constructor.Services.AddProblemDetails();
constructor.Services.AddExceptionHandler<ManejadorDeExcepcionesDeDominio>();

var app = constructor.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // AddOpenApi solo genera el documento; quien lo pinta y deja lanzar peticiones es
    // Scalar. Queda en /scalar y solo en desarrollo.
    app.MapScalarApiReference(opciones => opciones.Title = "Trasiego");

    // En desarrollo se migra al arrancar para no tener que acordarse de hacerlo a mano.
    // En produccion las migraciones se aplican en el despliegue, no desde la aplicacion.
    using var ambito = app.Services.CreateScope();
    await ambito.ServiceProvider.GetRequiredService<ContextoDeTrasiego>().Database.MigrateAsync();
}

app.MapGet("/salud", async Task<Results<Ok<EstadoDeSalud>, ProblemHttpResult>> (
    ContextoDeTrasiego contexto) =>
    await contexto.Database.CanConnectAsync()
        ? TypedResults.Ok(new EstadoDeSalud("vivo"))
        : TypedResults.Problem("No se llega a la base de datos.", statusCode: 503));

app.MapControllers();

app.Run();

// Para que las pruebas de integracion puedan levantar la Api con WebApplicationFactory.
public partial class Program;
