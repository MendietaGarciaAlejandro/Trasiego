namespace Trasiego.Aplicacion.Abstracciones;

/// <summary>
/// Quien esta detras de lo que se registra ahora mismo.
/// </summary>
/// <remarks>
/// No se pasa como argumento a cada metodo aposta. Registrar una entrada pide un articulo,
/// un almacen, una cantidad y un coste: quien la teclea no es un dato mas de la entrada, es
/// el contexto en el que ocurre todo lo de esta peticion. Metido como parametro habria que
/// arrastrarlo por las ocho operaciones del servicio y por dentro de cada una hasta el sitio
/// donde se crea el movimiento.
///
/// En la Api sale del token. Fuera de una peticion no hay nadie, y devuelve null.
/// </remarks>
public interface IQuienRegistra
{
    Guid? Id { get; }
}
