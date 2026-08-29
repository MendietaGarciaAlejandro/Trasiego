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

    public int HorasDeValidez { get; set; } = 8;
}
