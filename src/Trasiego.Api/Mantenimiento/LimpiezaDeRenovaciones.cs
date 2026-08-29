using Trasiego.Aplicacion.Acceso;

namespace Trasiego.Api.Mantenimiento;

/// <summary>
/// Pasa cada tantas horas a tirar las renovaciones caducadas. Nadie las va a usar ya, pero
/// si no las quita alguien la tabla no para de crecer: se apunta una cada vez que alguien
/// entra o renueva, y renovar pasa cada cuarto de hora mientras se trabaja.
/// </summary>
public class LimpiezaDeRenovaciones(
    IServiceScopeFactory ambitos,
    TimeProvider reloj,
    ILogger<LimpiezaDeRenovaciones> registro) : BackgroundService
{
    private static readonly TimeSpan CadaCuanto = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        using var cuandoToque = new PeriodicTimer(CadaCuanto, reloj);

        try
        {
            do
            {
                await Limpiar(parada);
            }
            while (await cuandoToque.WaitForNextTickAsync(parada));
        }
        catch (OperationCanceledException)
        {
            // Se esta apagando la aplicacion.
        }
    }

    private async Task Limpiar(CancellationToken parada)
    {
        try
        {
            using var ambito = ambitos.CreateScope();
            var acceso = ambito.ServiceProvider.GetRequiredService<ServicioDeAcceso>();

            var cuantas = await acceso.LimpiarRenovacionesCaducadas(parada);
            if (cuantas > 0) registro.LogInformation("Renovaciones caducadas tiradas: {Cuantas}", cuantas);
        }
        catch (Exception fallo) when (fallo is not OperationCanceledException)
        {
            // Que no se pueda limpiar no es motivo para tumbar la aplicacion: se apunta y se
            // vuelve a intentar en la siguiente vuelta.
            registro.LogWarning(fallo, "No se han podido tirar las renovaciones caducadas.");
        }
    }
}
