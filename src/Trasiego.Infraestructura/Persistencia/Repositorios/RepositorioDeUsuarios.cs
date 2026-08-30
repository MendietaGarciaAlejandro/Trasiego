using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeUsuarios(ContextoDeTrasiego contexto) : IRepositorioDeUsuarios
{
    public Task<Usuario?> PorId(Guid id, CancellationToken cancelacion = default) =>
        contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancelacion);

    public Task<Usuario?> PorCorreo(string correo, CancellationToken cancelacion = default) =>
        contexto.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo, cancelacion);

    public async Task<IReadOnlyList<Usuario>> Listar(CancellationToken cancelacion = default) =>
        await contexto.Usuarios.OrderBy(u => u.Correo).ToListAsync(cancelacion);

    public async Task<IReadOnlyDictionary<Guid, string>> NombresDe(
        IEnumerable<Guid> ids,
        CancellationToken cancelacion = default) =>
        await contexto.Usuarios
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, cancelacion);

    public async Task Alta(Usuario usuario, CancellationToken cancelacion = default)
    {
        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync(cancelacion);
    }
}
