using System.Net;
using Trasiego.Interfaz.Cliente;

namespace Trasiego.Escritorio.Sesion;

/// <summary>
/// Hace que cerrar la ventana no sea salir.
/// </summary>
/// <remarks>
/// En el navegador esto lo resuelve el propio navegador: la cookie de renovacion sobrevive a
/// un recargado sin que nadie haga nada. Aqui el que guarda las cookies es el HttpClient, y
/// eso se muere con la aplicacion, asi que hay que sacar la renovacion del contenedor y
/// dejarla en el administrador de credenciales de Windows.
///
/// Lo que se guarda es la misma renovacion que iria en la cookie: dura una semana, se gasta
/// en cuanto se usa y se cambia por otra. Aqui no se guarda nunca la contraseña.
/// </remarks>
public class MemoriaDeSesion
{
    private const string Nombre = "Trasiego";
    private const string Galleta = "trasiego_renovacion";
    private const string Camino = "/api/acceso";

    private readonly CookieContainer _galletas;
    private readonly ClienteDeTrasiego _trasiego;
    private readonly Uri _donde;

    public MemoriaDeSesion(CookieContainer galletas, ClienteDeTrasiego trasiego, Uri donde)
    {
        _galletas = galletas;
        _trasiego = trasiego;
        _donde = donde;

        _trasiego.SesionCambiada += Apuntar;
    }

    /// <summary>
    /// Devuelve al contenedor la renovacion de la ultima vez, para que al arrancar se pueda
    /// seguir donde se dejo.
    /// </summary>
    public void Recordar()
    {
        if (AlmacenDeCredenciales.Leer(Nombre) is not { } renovacion) return;

        _galletas.Add(new Cookie(Galleta, renovacion, Camino, _donde.Host));
    }

    private void Apuntar()
    {
        if (!_trasiego.HaEntrado)
        {
            AlmacenDeCredenciales.Olvidar(Nombre);
            return;
        }

        // La respuesta ya ha pasado por el contenedor, asi que aqui esta la renovacion nueva:
        // cada vez que se renueva, la anterior se gasta y hay que guardar la que la sustituye.
        var actual = _galletas
            .GetCookies(new Uri(_donde, Camino))
            .FirstOrDefault(galleta => galleta.Name == Galleta);

        if (actual is not null) AlmacenDeCredenciales.Guardar(Nombre, actual.Value);
    }
}
