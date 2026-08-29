using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Acceso;

namespace Trasiego.Infraestructura.Persistencia;

/// <summary>
/// Deja un par de usuarios con los que poder entrar en desarrollo. Sin esto, la primera vez
/// que se arranca no hay forma de identificarse y tampoco de crear a nadie, porque dar de
/// alta usuarios ya pide estar identificado.
/// </summary>
public static class SembradorDeDesarrollo
{
    public const string Contrasena = "trasiego-demo-2026";

    public static async Task Sembrar(
        ContextoDeTrasiego contexto,
        IHuellaDeContrasenas huellas,
        CancellationToken cancelacion = default)
    {
        if (await contexto.Usuarios.AnyAsync(cancelacion)) return;

        contexto.Usuarios.AddRange(
            new Usuario(
                "encargada@trasiego.test", "Encargada de almacen",
                huellas.Calcular(Contrasena), RolDeUsuario.Responsable),
            new Usuario(
                "operario@trasiego.test", "Operario de almacen",
                huellas.Calcular(Contrasena), RolDeUsuario.Operario));

        await contexto.SaveChangesAsync(cancelacion);
    }
}
