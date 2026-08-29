using Microsoft.Extensions.Time.Testing;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class MovimientosEnBaseDeDatosTests(BaseDeDatosDePruebas baseDeDatos)
{
    private static readonly DateTimeOffset Ahora = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 3, 15);

    // Cada test se monta su articulo y su almacen, porque la base de datos es la misma para
    // toda la coleccion y con referencias fijas se pisarian unos a otros.
    private static int _siguiente;

    [Fact]
    public async Task Una_entrada_suma_al_saldo_y_una_salida_descuenta()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Catalogo(contexto);
        var servicio = Servicio(contexto);

        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(100), Hoy);
        await servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(30), Hoy);

        var saldo = await new RepositorioDeMovimientos(contexto).Saldo(articulo.Id, almacen.Id);

        Assert.Equal(Cantidad.De(70), saldo);
    }

    [Fact]
    public async Task No_sale_mas_de_lo_que_hay_y_el_aviso_dice_cuanto_queda()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Catalogo(contexto);
        var servicio = Servicio(contexto);

        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(10), Hoy);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(11), Hoy));

        Assert.Contains("quedan 10 ud", fallo.Message);
    }

    [Fact]
    public async Task Un_movimiento_con_fecha_futura_no_entra()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Catalogo(contexto);
        var servicio = Servicio(contexto);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(1), Hoy.AddDays(1)));

        Assert.Equal("No se puede registrar un movimiento con fecha futura.", fallo.Message);
    }

    [Fact]
    public async Task El_saldo_a_fecha_no_cuenta_lo_que_vino_despues()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Catalogo(contexto);
        var servicio = Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);

        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(40), Hoy.AddDays(-5));
        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(60), Hoy);

        Assert.Equal(Cantidad.De(40), await movimientos.Saldo(articulo.Id, almacen.Id, Hoy.AddDays(-1)));
        Assert.Equal(Cantidad.De(100), await movimientos.Saldo(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Un_movimiento_retroactivo_cambia_el_saldo_de_un_dia_ya_pasado()
    {
        // Aqui es donde sirve tener separadas la fecha contable y el momento de registro:
        // el albaran se teclea hoy, pero el saldo del dia 10 pasa a ser otro.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Catalogo(contexto);
        var servicio = Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);

        var elDiaDiez = Hoy.AddDays(-5);
        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(40), elDiaDiez);
        Assert.Equal(Cantidad.De(40), await movimientos.Saldo(articulo.Id, almacen.Id, elDiaDiez));

        var traspapelado = await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(25), elDiaDiez, "albaran traspapelado");

        Assert.Equal(Cantidad.De(65), await movimientos.Saldo(articulo.Id, almacen.Id, elDiaDiez));
        Assert.Equal(elDiaDiez, traspapelado.FechaContable);
        Assert.Equal(Ahora, traspapelado.MomentoDeRegistro);
    }

    [Fact]
    public async Task Un_articulo_de_baja_no_admite_movimientos()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Catalogo(contexto);

        articulo.DarDeBaja();
        await new RepositorioDeArticulos(contexto).GuardarCambios();

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Servicio(contexto).RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(1), Hoy));

        Assert.Contains("esta de baja", fallo.Message);
    }

    [Fact]
    public async Task Los_movimientos_salen_en_el_orden_en_que_cuentan()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Catalogo(contexto);
        var servicio = Servicio(contexto);

        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(5), Hoy);
        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(9), Hoy.AddDays(-3));

        var listado = await new RepositorioDeMovimientos(contexto).Listar(articulo.Id, almacen.Id);

        Assert.Equal([Cantidad.De(9), Cantidad.De(5)], listado.Select(m => m.Cantidad));
    }

    private static ServicioDeMovimientos Servicio(ContextoDeTrasiego contexto) =>
        new(new RepositorioDeArticulos(contexto),
            new RepositorioDeAlmacenes(contexto),
            new RepositorioDeMovimientos(contexto),
            new FakeTimeProvider(Ahora));

    private static async Task<(Articulo, Almacen)> Catalogo(
        ContextoDeTrasiego contexto,
        UnidadDeMedida unidad = UnidadDeMedida.Unidad)
    {
        var numero = Interlocked.Increment(ref _siguiente);

        var articulo = new Articulo($"ART-{numero}", $"Articulo {numero}", unidad);
        await new RepositorioDeArticulos(contexto).Alta(articulo);

        var almacen = new Almacen($"A{numero}", $"Almacen {numero}");
        await new RepositorioDeAlmacenes(contexto).Alta(almacen);

        return (articulo, almacen);
    }
}
