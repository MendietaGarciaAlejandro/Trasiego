using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Trasiego.Contratos;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Interfaz.Cliente;

/// <summary>Lo que la Api ha contestado que no se puede hacer, con su motivo.</summary>
public class FalloDeTrasiego(string mensaje) : Exception(mensaje);

public class ClienteDeTrasiego(HttpClient http)
{
    // Las mismas opciones que la Api: alli los enums salen por su nombre.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private string? _token;

    public string? Nombre { get; private set; }

    public RolDeUsuario? Rol { get; private set; }

    public bool HaEntrado => _token is not null;

    public bool EsResponsable => Rol is RolDeUsuario.Responsable;

    /// <summary>
    /// Salta al entrar y al salir. Quien decide si se enseña la portada o la aplicacion no
    /// es el mismo componente que cierra la sesion, asi que sin avisar se quedaria pintado
    /// lo de antes.
    /// </summary>
    public event Action? SesionCambiada;

    public async Task Entrar(string correo, string contrasena)
    {
        // Sin token todavia, asi que esta peticion va suelta a proposito.
        Apuntar(await Mandar<AccesoPedido, EntradaVista>(
            "api/acceso", new AccesoPedido(correo, contrasena)));
    }

    /// <summary>
    /// Intenta seguir donde se dejo. La renovacion viaja sola en su cookie, asi que esto no
    /// manda nada: o hay sesion o no la hay.
    /// </summary>
    public async Task<bool> Retomar()
    {
        try
        {
            Apuntar(await Renovar());
            return true;
        }
        catch (FalloDeTrasiego)
        {
            return false;
        }
    }

    /// <summary>
    /// El token se queda en memoria y no se guarda en ningun sitio, asi que cerrar la
    /// aplicacion es salir. Guardarlo seria comodo, pero un token en el almacenamiento del
    /// navegador se lo lleva cualquiera que consiga meter un script en la pagina.
    /// </summary>
    public async Task Salir()
    {
        try
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Post, "api/acceso/salir");
            await http.SendAsync(peticion);
        }
        catch (HttpRequestException)
        {
            // Si no se llega al servidor, al menos aqui se olvida.
        }

        Olvidar();
    }

    private void Olvidar()
    {
        _token = null;
        Nombre = null;
        Rol = null;

        SesionCambiada?.Invoke();
    }

    private void Apuntar(EntradaVista entrada)
    {
        _token = entrada.Token;
        Nombre = entrada.Nombre;
        Rol = entrada.Rol;

        SesionCambiada?.Invoke();
    }

    private async Task<EntradaVista> Renovar()
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "api/acceso/renovar");
        return await Leer<EntradaVista>(await http.SendAsync(peticion));
    }

    public Task<IReadOnlyList<ArticuloVisto>> Articulos(bool incluirBajas = false) =>
        Traer<IReadOnlyList<ArticuloVisto>>($"api/articulos?incluirBajas={incluirBajas}");

    public Task<ArticuloVisto> AltaDeArticulo(AltaDeArticulo peticion) =>
        Mandar<AltaDeArticulo, ArticuloVisto>("api/articulos", peticion);

    public Task<IReadOnlyList<AlmacenVisto>> Almacenes(bool incluirBajas = false) =>
        Traer<IReadOnlyList<AlmacenVisto>>($"api/almacenes?incluirBajas={incluirBajas}");

    public Task<AlmacenVisto> AltaDeAlmacen(AltaDeAlmacen peticion) =>
        Mandar<AltaDeAlmacen, AlmacenVisto>("api/almacenes", peticion);

    public Task<IReadOnlyList<LineaDeKardex>> Kardex(Guid articuloId, Guid almacenId) =>
        Traer<IReadOnlyList<LineaDeKardex>>(
            $"api/movimientos/kardex?articuloId={articuloId}&almacenId={almacenId}");

    public Task<ExistenciasVistas> Existencias(Guid articuloId, Guid almacenId) =>
        Traer<ExistenciasVistas>(
            $"api/movimientos/existencias?articuloId={articuloId}&almacenId={almacenId}");

    public Task<MovimientoVisto> Entrada(EntradaPedida peticion) =>
        Mandar<EntradaPedida, MovimientoVisto>("api/movimientos/entradas", peticion);

    public Task<MovimientoVisto> Salida(SalidaPedida peticion) =>
        Mandar<SalidaPedida, MovimientoVisto>("api/movimientos/salidas", peticion);

    public Task<IReadOnlyList<DocumentoVisto>> Documentos(Guid almacenId) =>
        Traer<IReadOnlyList<DocumentoVisto>>($"api/documentos?almacenId={almacenId}");

    public Task<DocumentoVisto> Documento(Guid id) =>
        Traer<DocumentoVisto>($"api/documentos/{id}");

    public Task<DocumentoVisto> AbrirDocumento(AbrirDocumento peticion) =>
        Mandar<AbrirDocumento, DocumentoVisto>("api/documentos", peticion);

    public Task<DocumentoVisto> AgregarLinea(Guid documentoId, LineaPedida peticion) =>
        Mandar<LineaPedida, DocumentoVisto>($"api/documentos/{documentoId}/lineas", peticion);

    public async Task<DocumentoVisto> QuitarLinea(Guid documentoId, Guid lineaId)
    {
        using var peticion = new HttpRequestMessage(
            HttpMethod.Delete, $"api/documentos/{documentoId}/lineas/{lineaId}");

        return await Leer<DocumentoVisto>(await Enviar(peticion));
    }

    public async Task<IReadOnlyList<MovimientoVisto>> RegistrarDocumento(Guid documentoId)
    {
        using var peticion = new HttpRequestMessage(
            HttpMethod.Post, $"api/documentos/{documentoId}/registrar");

        return await Leer<IReadOnlyList<MovimientoVisto>>(await Enviar(peticion));
    }

    public Task<TraspasoVisto> Traspaso(TraspasoPedido peticion) =>
        Mandar<TraspasoPedido, TraspasoVisto>("api/movimientos/traspasos", peticion);

    public Task<ValoracionVista> Valoracion(Guid almacenId, DateOnly fecha) =>
        Traer<ValoracionVista>(
            $"api/informes/valoracion?almacenId={almacenId}&fecha={fecha:yyyy-MM-dd}");

    public Task<IReadOnlyList<CierreVisto>> Cierres(Guid almacenId) =>
        Traer<IReadOnlyList<CierreVisto>>($"api/cierres?almacenId={almacenId}");

    public Task<CierreVisto> Cerrar(CierrePedido peticion) =>
        Mandar<CierrePedido, CierreVisto>("api/cierres", peticion);

    public Task<IReadOnlyList<DescuadreVisto>> Comprobar(Guid cierreId) =>
        Traer<IReadOnlyList<DescuadreVisto>>($"api/cierres/{cierreId}/comprobacion");

    public Task<IReadOnlyList<Guid>> Sospechosos(Guid almacenId) =>
        Traer<IReadOnlyList<Guid>>($"api/recalculo/sospechosos?almacenId={almacenId}");

    public Task<ReproduccionVista> Comparar(Guid articuloId, Guid almacenId) =>
        Traer<ReproduccionVista>(
            $"api/recalculo/comparacion?articuloId={articuloId}&almacenId={almacenId}");

    public async Task<ReproduccionVista> Aplicar(Guid articuloId, Guid almacenId)
    {
        using var peticion = new HttpRequestMessage(
            HttpMethod.Post, $"api/recalculo?articuloId={articuloId}&almacenId={almacenId}");

        return await Leer<ReproduccionVista>(await Enviar(peticion));
    }

    private async Task<T> Traer<T>(string ruta)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        return await Leer<T>(await Enviar(peticion));
    }

    private async Task<TRespuesta> Mandar<TPeticion, TRespuesta>(string ruta, TPeticion cuerpo)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = JsonContent.Create(cuerpo, options: Json),
        };

        return await Leer<TRespuesta>(await Enviar(peticion));
    }

    private async Task<HttpResponseMessage> Enviar(HttpRequestMessage peticion)
    {
        var respuesta = await Firmar(peticion);
        if (respuesta.StatusCode is not HttpStatusCode.Unauthorized) return respuesta;

        // El token de acceso dura poco. Cuando caduca a media faena se renueva y se repite
        // la peticion, y quien esta trabajando no se entera de nada.
        respuesta.Dispose();

        try
        {
            Apuntar(await Renovar());
        }
        catch (FalloDeTrasiego)
        {
            Olvidar();
            throw new FalloDeTrasiego("La sesion ha caducado. Vuelve a entrar.");
        }

        using var otraVez = await Repetir(peticion);
        return await Firmar(otraVez);
    }

    private Task<HttpResponseMessage> Firmar(HttpRequestMessage peticion)
    {
        if (_token is not null)
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        return http.SendAsync(peticion);
    }

    /// <summary>
    /// Un HttpRequestMessage no se puede mandar dos veces, asi que para reintentar hay que
    /// hacer otro igual.
    /// </summary>
    private static async Task<HttpRequestMessage> Repetir(HttpRequestMessage original)
    {
        var copia = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content is not null)
            copia.Content = new StringContent(
                await original.Content.ReadAsStringAsync(),
                System.Text.Encoding.UTF8,
                "application/json");

        return copia;
    }

    private static async Task<T> Leer<T>(HttpResponseMessage respuesta)
    {
        if (!respuesta.IsSuccessStatusCode) throw new FalloDeTrasiego(await Motivo(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>
    /// El detalle del ProblemDetails, que es el mensaje que escribio quien puso la regla. La
    /// pantalla lo enseña tal cual: para eso se escribio pensando en quien lo iba a leer.
    /// </summary>
    private static async Task<string> Motivo(HttpResponseMessage respuesta)
    {
        try
        {
            using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            if (problema.RootElement.TryGetProperty("detail", out var detalle))
                return detalle.GetString() ?? "";
        }
        catch (JsonException)
        {
            // Si lo que ha vuelto no es un ProblemDetails, no hay motivo que enseñar.
        }

        return $"La aplicacion ha respondido {(int)respuesta.StatusCode}.";
    }
}
