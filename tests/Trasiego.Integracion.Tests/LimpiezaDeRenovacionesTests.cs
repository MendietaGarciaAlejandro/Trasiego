using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Acceso;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class LimpiezaDeRenovacionesTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Se_van_las_caducadas_y_se_quedan_las_que_todavia_valen()
    {
        await using var contexto = baseDeDatos.Contexto();
        var usuario = await Alguien(contexto);

        var caducada = Renovacion(usuario, Escenario.Ahora.AddDays(-1));
        var buena = Renovacion(usuario, Escenario.Ahora.AddDays(7));

        contexto.Renovaciones.AddRange(caducada, buena);
        await contexto.SaveChangesAsync();

        var tiradas = await new RepositorioDeTokens(contexto).BorrarCaducadas(Escenario.Ahora);

        Assert.Equal(1, tiradas);
        Assert.Equal(
            [buena.Id],
            await contexto.Renovaciones
                .Where(t => t.UsuarioId == usuario.Id)
                .Select(t => t.Id)
                .ToListAsync());
    }

    [Fact]
    public async Task Una_gastada_que_todavia_no_ha_caducado_se_queda()
    {
        // Es lo que delata que alguien tiene una copia: si reaparece, se tiran todas. Tirarla
        // antes de tiempo seria quedarse sin ese aviso.
        await using var contexto = baseDeDatos.Contexto();
        var usuario = await Alguien(contexto);

        var gastada = Renovacion(usuario, Escenario.Ahora.AddDays(7));
        gastada.Usar();

        contexto.Renovaciones.Add(gastada);
        await contexto.SaveChangesAsync();

        await new RepositorioDeTokens(contexto).BorrarCaducadas(Escenario.Ahora);

        Assert.True(await contexto.Renovaciones.AnyAsync(t => t.Id == gastada.Id));
    }

    private static int _siguiente;

    private static async Task<Usuario> Alguien(ContextoDeTrasiego contexto)
    {
        var numero = Interlocked.Increment(ref _siguiente);
        var usuario = new Usuario(
            $"limpieza{numero}@trasiego.test", $"Quien sea {numero}",
            "una-huella-cualquiera", RolDeUsuario.Operario);

        await new RepositorioDeUsuarios(contexto).Alta(usuario);
        return usuario;
    }

    private static TokenDeRenovacion Renovacion(Usuario usuario, DateTimeOffset caduca) =>
        new(usuario.Id, Guid.CreateVersion7().ToString("N"), caduca);
}
