namespace Trasiego.Dominio.Comun;

/// <summary>
/// Base de los fallos de negocio. La capa Api las traduce a respuestas HTTP y la de
/// escritorio a un aviso en pantalla; ninguna de las dos tiene que conocer los detalles.
/// </summary>
public abstract class ExcepcionDeDominio(string mensaje) : Exception(mensaje);

/// <summary>No existe lo que se pide.</summary>
public sealed class NoEncontrado(string mensaje) : ExcepcionDeDominio(mensaje);

/// <summary>Se incumple una regla de negocio.</summary>
public sealed class ReglaDeNegocio(string mensaje) : ExcepcionDeDominio(mensaje);

/// <summary>La operacion choca con el estado actual de los datos.</summary>
public sealed class Conflicto(string mensaje) : ExcepcionDeDominio(mensaje);
