using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeUsuarios(ContextoDeTrasiego contexto) : IRepositorioDeUsuarios
{
    public Task<Usuario?> PorCorreo(string correo, CancellationToken cancelacion = default) =>
        contexto.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo, cancelacion);

    public async Task<IReadOnlyList<Usuario>> Listar(CancellationToken cancelacion = default) =>
        await contexto.Usuarios.OrderBy(u => u.Correo).ToListAsync(cancelacion);

    public async Task Alta(Usuario usuario, CancellationToken cancelacion = default)
    {
        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync(cancelacion);
    }
}
