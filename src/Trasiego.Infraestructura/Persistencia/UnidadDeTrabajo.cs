using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Comun;

namespace Trasiego.Infraestructura.Persistencia;

public class UnidadDeTrabajo(ContextoDeTrasiego contexto) : IUnidadDeTrabajo
{
    /// <summary>
    /// Cuantas veces se vuelve a intentar antes de rendirse. Si con estas no ha entrado es
    /// que hay tanta gente peleando por el mismo articulo que mas vale avisar que seguir
    /// dando vueltas.
    /// </summary>
    private const int Intentos = 8;

    public Task GuardarCambios(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);

    public async Task<T> ConReintentos<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancelacion = default)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await operacion(cancelacion);
            }
            catch (DbUpdateConcurrencyException) when (intento < Intentos)
            {
                // Lo que quedo cargado ya no vale: alguien lo ha cambiado por debajo. Se
                // olvida todo para que el siguiente intento vuelva a leerlo de la base.
                contexto.ChangeTracker.Clear();

                // Y se espera un poco antes de volver. Sin esta espera los que han chocado
                // reintentan todos a la vez y se vuelven a estorbar: con diez peticiones
                // peleando por la misma capa se agotaban los intentos sin que entrara nadie.
                // El rato es distinto para cada uno aposta, para que no vuelvan en bloque.
                await Task.Delay(Random.Shared.Next(10, 40) * intento, cancelacion);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Conflicto(
                    "Hay demasiado movimiento sobre ese articulo ahora mismo. Vuelve a intentarlo.");
            }
        }
    }
}
