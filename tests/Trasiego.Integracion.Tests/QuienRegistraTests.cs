using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class QuienRegistraTests(BaseDeDatosDePruebas baseDeDatos)
{
    private static int _siguiente;

    [Fact]
    public async Task Cada_movimiento_se_queda_con_quien_lo_registro()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var quien = await Alguien(contexto);
        var servicio = Escenario.Servicio(contexto, quien.Id);

        var entrada = await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy);

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy);

        Assert.Equal(quien.Id, entrada.UsuarioId);
        Assert.Equal(quien.Id, salida.UsuarioId);
    }

    [Fact]
    public async Task Los_movimientos_derivados_los_firma_el_que_los_provoco()
    {
        // Una devolucion y las dos mitades de un traspaso no las teclea nadie linea a linea,
        // pero alguien pulso el boton y es el que responde de ellas.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await Escenario.OtroAlmacen(contexto);
        var quien = await Alguien(contexto);
        var servicio = Escenario.Servicio(contexto, quien.Id);

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-1));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, origen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-1));

        var devolucion = await servicio.DevolverSalida(
            salida.Id, Cantidad.De(1), Escenario.Hoy);

        var traspaso = await servicio.Traspasar(
            articulo.Id, origen.Id, destino.Id, Cantidad.De(2), Escenario.Hoy);

        Assert.Equal(quien.Id, devolucion.UsuarioId);
        Assert.Equal(quien.Id, traspaso.Salida.UsuarioId);
        Assert.Equal(quien.Id, traspaso.Entrada.UsuarioId);
    }

    [Fact]
    public async Task El_kardex_enseña_el_nombre_y_no_el_identificador()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var quien = await Alguien(contexto);

        await Escenario.Servicio(contexto, quien.Id).RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy);

        // Y uno sin nadie detras, de los que puede meter un proceso o los que quedaron de
        // antes de que hubiera usuarios.
        await Escenario.Servicio(contexto).RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(3m), Escenario.Hoy);

        var kardex = await Escenario.Servicio(contexto).Kardex(articulo.Id, almacen.Id);

        Assert.Equal(quien.Nombre, kardex[0].Usuario);
        Assert.Null(kardex[1].Usuario);
    }

    [Fact]
    public async Task Un_usuario_con_movimientos_no_se_puede_borrar()
    {
        // La firma no vale de nada si se puede hacer desaparecer al que firmo. Los usuarios
        // se dan de baja, y la base de datos ademas no deja borrarlos.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var quien = await Alguien(contexto);

        await Escenario.Servicio(contexto, quien.Id).RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(2m), Escenario.Hoy);

        // Otro contexto aposta: quien tiene que negarse es la base de datos, y con el
        // movimiento cargado EF le quitaria la firma antes de intentar el borrado.
        await using var otro = baseDeDatos.Contexto();
        otro.Usuarios.Remove(await otro.Usuarios.SingleAsync(u => u.Id == quien.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => otro.SaveChangesAsync());
    }

    private static async Task<Usuario> Alguien(ContextoDeTrasiego contexto)
    {
        var numero = Interlocked.Increment(ref _siguiente);

        var usuario = new Usuario(
            $"quien{numero}@trasiego.test", $"Quien {numero}", "da igual", RolDeUsuario.Operario);

        await new RepositorioDeUsuarios(contexto).Alta(usuario);
        return usuario;
    }
}
