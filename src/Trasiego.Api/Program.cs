using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Trasiego.Api.Errores;
using Trasiego.Api.Mantenimiento;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Aplicacion.Acceso;
using Trasiego.Aplicacion.Almacenes;
using Trasiego.Aplicacion.Catalogo;
using Trasiego.Aplicacion.Cierres;
using Trasiego.Aplicacion.Informes;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Aplicacion.Valoracion;
using Trasiego.Contratos;
using Trasiego.Infraestructura;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Seguridad;

var constructor = WebApplication.CreateBuilder(args);

var cadenaDeConexion = constructor.Configuration.GetConnectionString("Trasiego")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexion 'Trasiego'. En desarrollo esta en appsettings.Development.json.");

constructor.Services.AgregarInfraestructura(cadenaDeConexion);

constructor.Services.Configure<OpcionesDeToken>(
    constructor.Configuration.GetSection(OpcionesDeToken.Seccion));

var jwt = constructor.Configuration.GetSection(OpcionesDeToken.Seccion).Get<OpcionesDeToken>()
    ?? throw new InvalidOperationException("Falta la seccion 'Jwt' de configuracion.");

if (string.IsNullOrWhiteSpace(jwt.Clave))
    throw new InvalidOperationException(
        "Falta 'Jwt:Clave'. Ponla con: dotnet user-secrets set \"Jwt:Clave\" \"<una clave larga>\" " +
        "--project src/Trasiego.Api");

constructor.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones => opciones.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Emisor,
        ValidAudience = jwt.Audiencia,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),

        // Sin margen extra: un token caducado deja de valer al segundo.
        ClockSkew = TimeSpan.Zero,
    });

constructor.Services.AddAuthorization();

constructor.Services
    .AddControllers()
    .AddJsonOptions(opciones =>
        // Los enums viajan por su nombre. Un "Fifo" se entiende leyendo la respuesta; un 1 no,
        // y ademas ata al cliente al orden en que estan declarados.
        opciones.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

constructor.Services.AddOpenApi();

constructor.Services.AddScoped<ServicioDeAcceso>();
constructor.Services.AddScoped<ServicioDeArticulos>();
constructor.Services.AddScoped<ServicioDeAlmacenes>();
constructor.Services.AddScoped<ServicioDeMovimientos>();
constructor.Services.AddScoped<ServicioDeCierres>();
constructor.Services.AddScoped<ServicioDeInformes>();
constructor.Services.AddScoped<ServicioDeRecalculo>();
constructor.Services.AddSingleton(TimeProvider.System);
constructor.Services.AddHostedService<LimpiezaDeRenovaciones>();

constructor.Services.AddProblemDetails();
constructor.Services.AddExceptionHandler<ManejadorDeExcepcionesDeDominio>();

var app = constructor.Build();

app.UseExceptionHandler();

// La Api sirve tambien el cliente web. Es la misma aplicacion de siempre: las pantallas
// hablan con la Api por HTTP igual que en el escritorio, solo que aqui las aloja el
// navegador y no un WebView. Al ser el mismo origen no hace falta CORS.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // AddOpenApi solo genera el documento; quien lo pinta y deja lanzar peticiones es
    // Scalar. Queda en /scalar y solo en desarrollo.
    app.MapScalarApiReference(opciones => opciones.Title = "Trasiego");

    // En desarrollo se migra al arrancar para no tener que acordarse de hacerlo a mano.
    // En produccion las migraciones se aplican en el despliegue, no desde la aplicacion.
    using var ambito = app.Services.CreateScope();
    var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDeTrasiego>();
    await contexto.Database.MigrateAsync();

    await SembradorDeDesarrollo.Sembrar(
        contexto, ambito.ServiceProvider.GetRequiredService<IHuellaDeContrasenas>());
}

app.MapGet("/salud", async Task<Results<Ok<EstadoDeSalud>, ProblemHttpResult>> (
    ContextoDeTrasiego contexto) =>
    await contexto.Database.CanConnectAsync()
        ? TypedResults.Ok(new EstadoDeSalud("vivo"))
        : TypedResults.Problem("No se llega a la base de datos.", statusCode: 503));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Lo que no sea la Api ni el documento de OpenAPI es una ruta del cliente web, y de eso ya
// se encarga el enrutado de Blazor dentro del navegador.
app.MapFallbackToFile("index.html");

app.Run();

// Para que las pruebas de integracion puedan levantar la Api con WebApplicationFactory.
public partial class Program;
