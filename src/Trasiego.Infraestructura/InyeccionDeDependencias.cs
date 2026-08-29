using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;
using Trasiego.Infraestructura.Seguridad;

namespace Trasiego.Infraestructura;

public static class InyeccionDeDependencias
{
    /// <summary>
    /// Recibe la cadena de conexion ya resuelta, y no un IConfiguration, para que esta capa
    /// no tenga que saber de donde sale: en la Api viene de user-secrets y en las pruebas
    /// de una base de datos que se crea y se tira en cada ejecucion.
    /// </summary>
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        servicios.AddDbContext<ContextoDeTrasiego>(opciones =>
            opciones.UseSqlServer(cadenaDeConexion));

        servicios.AddScoped<IRepositorioDeArticulos, RepositorioDeArticulos>();
        servicios.AddScoped<IRepositorioDeAlmacenes, RepositorioDeAlmacenes>();
        servicios.AddScoped<IRepositorioDeMovimientos, RepositorioDeMovimientos>();
        servicios.AddScoped<IRepositorioDeValoracion, RepositorioDeValoracion>();
        servicios.AddScoped<IRepositorioDeCierres, RepositorioDeCierres>();
        servicios.AddScoped<IRepositorioDeUsuarios, RepositorioDeUsuarios>();
        servicios.AddScoped<IRepositorioDeTokens, RepositorioDeTokens>();
        servicios.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();

        servicios.AddSingleton<IHuellaDeContrasenas, HuellaBCrypt>();
        servicios.AddScoped<IGeneradorDeTokens, GeneradorDeTokens>();

        return servicios;
    }
}
