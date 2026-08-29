using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Trasiego.Dominio.Comun;

namespace Trasiego.Api.Errores;

/// <summary>
/// Traduce los fallos de negocio a respuestas HTTP. La idea es que el detalle sea el mismo
/// texto que escribio quien puso la regla: son mensajes pensados para que los lea alguien de
/// almacen, no para que los reescriba el cliente.
/// </summary>
public class ManejadorDeExcepcionesDeDominio(IProblemDetailsService problemas) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken cancelacion)
    {
        var (codigo, titulo) = Clasificar(excepcion);
        if (codigo is null) return false;

        contexto.Response.StatusCode = codigo.Value;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails
            {
                Status = codigo,
                Title = titulo,
                Detail = excepcion.Message,
            },
        });
    }

    private static (int? Codigo, string? Titulo) Clasificar(Exception excepcion) => excepcion switch
    {
        NoEncontrado => (StatusCodes.Status404NotFound, "No existe"),
        Conflicto => (StatusCodes.Status409Conflict, "Choca con el estado actual"),
        ReglaDeNegocio => (StatusCodes.Status422UnprocessableEntity, "Regla de almacen"),

        // Estas nacieron para avisar de errores de programacion, pero en el borde de la Api
        // las provoca quien manda una cantidad negativa o una referencia en blanco, y eso es
        // una peticion mal hecha y no un fallo del servidor.
        ArgumentException => (StatusCodes.Status400BadRequest, "Peticion mal formada"),

        _ => (null, null),
    };
}
