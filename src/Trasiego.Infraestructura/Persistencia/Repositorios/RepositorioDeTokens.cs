using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeTokens(ContextoDeTrasiego contexto) : IRepositorioDeTokens
{
    public void Agregar(TokenDeRenovacion token) => contexto.Renovaciones.Add(token);

    public Task<TokenDeRenovacion?> PorHuella(
        string huella,
        CancellationToken cancelacion = default) =>
        contexto.Renovaciones.FirstOrDefaultAsync(t => t.Huella == huella, cancelacion);

    public async Task RevocarLosDe(Guid usuarioId, CancellationToken cancelacion = default)
    {
        await contexto.Renovaciones
            .Where(t => t.UsuarioId == usuarioId && !t.Revocado)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.Revocado, true), cancelacion);

        // El borrado en bloque no pasa por el seguimiento, asi que lo que hubiera cargado se
        // olvida para que no se guarde encima con lo de antes.
        contexto.ChangeTracker.Clear();
    }

    public Task GuardarCambios(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
