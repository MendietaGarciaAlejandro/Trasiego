using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class MovimientosEnBaseDeDatosTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Una_entrada_suma_al_saldo_y_una_salida_descuenta()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(100), Importe.De(250m), Escenario.Hoy);
        await servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(30), Escenario.Hoy);

        var saldo = await new RepositorioDeMovimientos(contexto).Saldo(articulo.Id, almacen.Id);

        Assert.Equal(Cantidad.De(70), saldo);
    }

    [Fact]
    public async Task No_sale_mas_de_lo_que_hay_y_el_aviso_dice_cuanto_queda()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(30m), Escenario.Hoy);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(11), Escenario.Hoy));

        Assert.Contains("quedan 10 ud", fallo.Message);
    }

    [Fact]
    public async Task Un_movimiento_con_fecha_futura_no_entra()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(3m), Escenario.Hoy.AddDays(1)));

        Assert.Equal("No se puede registrar un movimiento con fecha futura.", fallo.Message);
    }

    [Fact]
    public async Task El_saldo_a_fecha_no_cuenta_lo_que_vino_despues()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);

        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(40), Importe.De(80m), Escenario.Hoy.AddDays(-5));
        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(60), Importe.De(120m), Escenario.Hoy);

        Assert.Equal(Cantidad.De(40), await movimientos.Saldo(articulo.Id, almacen.Id, Escenario.Hoy.AddDays(-1)));
        Assert.Equal(Cantidad.De(100), await movimientos.Saldo(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Un_movimiento_retroactivo_cambia_el_saldo_de_un_dia_ya_pasado()
    {
        // Aqui es donde sirve tener separadas la fecha contable y el momento de registro:
        // el albaran se teclea hoy, pero el saldo del dia 10 pasa a ser otro.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);

        var elDiaDiez = Escenario.Hoy.AddDays(-5);
        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(40), Importe.De(80m), elDiaDiez);
        Assert.Equal(Cantidad.De(40), await movimientos.Saldo(articulo.Id, almacen.Id, elDiaDiez));

        var traspapelado = await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(25), Importe.De(50m), elDiaDiez, "albaran traspapelado");

        Assert.Equal(Cantidad.De(65), await movimientos.Saldo(articulo.Id, almacen.Id, elDiaDiez));
        Assert.Equal(elDiaDiez, traspapelado.FechaContable);
        Assert.Equal(Escenario.Ahora, traspapelado.MomentoDeRegistro);
    }

    [Fact]
    public async Task Un_articulo_de_baja_no_admite_movimientos()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);

        articulo.DarDeBaja();
        await new RepositorioDeArticulos(contexto).GuardarCambios();

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Servicio(contexto).RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(2m), Escenario.Hoy));

        Assert.Contains("esta de baja", fallo.Message);
    }

    [Fact]
    public async Task Los_movimientos_salen_en_el_orden_en_que_cuentan()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy);
        await servicio.RegistrarEntrada(articulo.Id, almacen.Id, Cantidad.De(9), Importe.De(18m), Escenario.Hoy.AddDays(-3));

        var listado = await new RepositorioDeMovimientos(contexto).Listar(articulo.Id, almacen.Id);

        Assert.Equal([Cantidad.De(9), Cantidad.De(5)], listado.Select(m => m.Cantidad));
    }
}
