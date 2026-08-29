using Trasiego.Aplicacion.Abstracciones;

namespace Trasiego.Infraestructura.Persistencia;

public class UnidadDeTrabajo(ContextoDeTrasiego contexto) : IUnidadDeTrabajo
{
    public Task GuardarCambios(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
