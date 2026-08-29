namespace Trasiego.Infraestructura.Seguridad;

public class OpcionesDeToken
{
    public const string Seccion = "Jwt";

    public string Emisor { get; set; } = "";
    public string Audiencia { get; set; } = "";

    /// <summary>
    /// La clave con la que se firma. No va en appsettings: en desarrollo se pone con
    /// user-secrets y en produccion sale del entorno.
    /// </summary>
    public string Clave { get; set; } = "";

    /// <summary>
    /// Lo que dura el token de acceso. Poco a proposito: si alguien lo intercepta, deja de
    /// servirle enseguida, y quien esta trabajando ni se entera porque se renueva solo.
    /// </summary>
    public int MinutosDeAcceso { get; set; } = 15;

    /// <summary>Lo que se puede estar sin teclear la contraseña otra vez.</summary>
    public int DiasDeRenovacion { get; set; } = 7;
}
