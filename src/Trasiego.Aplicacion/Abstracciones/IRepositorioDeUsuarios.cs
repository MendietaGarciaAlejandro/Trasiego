using Trasiego.Dominio.Acceso;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeUsuarios
{
    Task<Usuario?> PorId(Guid id, CancellationToken cancelacion = default);

    Task<Usuario?> PorCorreo(string correo, CancellationToken cancelacion = default);

    Task<IReadOnlyList<Usuario>> Listar(CancellationToken cancelacion = default);

    Task Alta(Usuario usuario, CancellationToken cancelacion = default);
}

/// <summary>
/// Resume una contraseña y comprueba si una coincide con su resumen. Va aqui detras de una
/// interfaz porque el algoritmo es cosa de infraestructura y se cambiara antes que nada.
/// </summary>
public interface IHuellaDeContrasenas
{
    string Calcular(string contrasena);

    bool Coincide(string contrasena, string huella);
}

public interface IRepositorioDeTokens
{
    void Agregar(TokenDeRenovacion token);

    Task<TokenDeRenovacion?> PorHuella(string huella, CancellationToken cancelacion = default);

    /// <summary>Tira todos los de un usuario. Se usa al salir y cuando algo huele mal.</summary>
    Task RevocarLosDe(Guid usuarioId, CancellationToken cancelacion = default);

    /// <summary>
    /// Se lleva las renovaciones que ya habian caducado. Devuelve cuantas eran.
    /// </summary>
    Task<int> BorrarCaducadas(DateTimeOffset hasta, CancellationToken cancelacion = default);

    Task GuardarCambios(CancellationToken cancelacion = default);
}

public interface IGeneradorDeTokens
{
    /// <summary>El que viaja en cada peticion. Dura poco a proposito.</summary>
    string DeAcceso(Usuario usuario);

    /// <summary>
    /// Uno nuevo para renovar, con su huella. El token en claro solo lo ve quien lo pide;
    /// lo que se guarda es la huella.
    /// </summary>
    (string Token, string Huella) DeRenovacion();

    string HuellaDe(string token);

    TimeSpan LoQueDuraLaRenovacion { get; }
}
