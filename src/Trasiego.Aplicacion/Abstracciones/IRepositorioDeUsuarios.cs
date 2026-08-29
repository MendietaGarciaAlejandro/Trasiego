using Trasiego.Dominio.Acceso;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeUsuarios
{
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

public interface IGeneradorDeTokens
{
    string Para(Usuario usuario);
}
