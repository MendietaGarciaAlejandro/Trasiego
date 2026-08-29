using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class CierreDePeriodoTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Cerrar_deja_apuntado_lo_que_habia_a_esa_fecha()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-8));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-6));

        var cierre = await Escenario.Cierres(contexto)
            .Cerrar(almacen.Id, Escenario.Hoy.AddDays(-5), "cierre de febrero");

        var declarado = await contexto.SaldosDeCierre
            .SingleAsync(s => s.CierreId == cierre.Id && s.ArticuloId == articulo.Id);

        Assert.Equal(Saldo.De(6), declarado.Cantidad);
        Assert.Equal(Importe.De(12m), declarado.Valor);
    }

    [Fact]
    public async Task Despues_de_cerrar_no_entra_nada_con_fecha_de_dentro()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-8));

        await Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-5));

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarEntrada(
                articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(2m),
                Escenario.Hoy.AddDays(-6)));

        Assert.Contains("esta cerrado hasta el", fallo.Message);
    }

    [Fact]
    public async Task El_mismo_dia_del_cierre_tampoco_admite_nada()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-8));

        var hastaEl = Escenario.Hoy.AddDays(-5);
        await Escenario.Cierres(contexto).Cerrar(almacen.Id, hastaEl);

        await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarEntrada(
                articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(2m), hastaEl));
    }

    [Fact]
    public async Task Con_fecha_posterior_al_cierre_se_sigue_trabajando()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-8));
        await Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-5));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(3), Escenario.Hoy);

        // Y sigue valorando con las capas de antes del cierre, que no se han tocado.
        Assert.Equal(Importe.De(6m), salida.Coste);
    }

    [Fact]
    public async Task Un_almacen_cerrado_no_impide_trabajar_en_otro()
    {
        await using var contexto = baseDeDatos.Contexto();
        var cerrado = await Escenario.Catalogo(contexto);
        var abierto = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            cerrado.Articulo.Id, cerrado.Almacen.Id, Cantidad.De(5), Importe.De(10m),
            Escenario.Hoy.AddDays(-8));
        await Escenario.Cierres(contexto).Cerrar(cerrado.Almacen.Id, Escenario.Hoy.AddDays(-5));

        // El de al lado sigue admitiendo movimientos con esa misma fecha.
        var entrada = await servicio.RegistrarEntrada(
            abierto.Articulo.Id, abierto.Almacen.Id, Cantidad.De(5), Importe.De(10m),
            Escenario.Hoy.AddDays(-6));

        Assert.Equal(Cantidad.De(5), entrada.Cantidad);
    }

    [Fact]
    public async Task No_se_cierra_un_dia_que_no_ha_pasado()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (_, almacen) = await Escenario.Catalogo(contexto);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(1)));

        Assert.Equal("No se cierra un dia que todavia no ha pasado.", fallo.Message);
    }

    [Fact]
    public async Task No_se_cierra_hacia_atras_ni_dos_veces_el_mismo_dia()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (_, almacen) = await Escenario.Catalogo(contexto);
        var cierres = Escenario.Cierres(contexto);

        await cierres.Cerrar(almacen.Id, Escenario.Hoy.AddDays(-5));

        await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            cierres.Cerrar(almacen.Id, Escenario.Hoy.AddDays(-5)));
        await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            cierres.Cerrar(almacen.Id, Escenario.Hoy.AddDays(-9)));
    }

    [Fact]
    public async Task Un_cierre_recien_hecho_cuadra_consigo_mismo()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var cierres = Escenario.Cierres(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-8));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy.AddDays(-7));

        var cierre = await cierres.Cerrar(almacen.Id, Escenario.Hoy.AddDays(-5));

        // Y sigue cuadrando despues de trabajar por encima de la fecha de cierre.
        await servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy);

        Assert.Empty(await cierres.Comprobar(cierre.Id));
    }

    [Fact]
    public async Task El_valor_a_fecha_de_cierre_no_tiene_por_que_ser_el_de_ahora()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-8));

        var cierre = await Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-5));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(90m), Escenario.Hoy);

        var declarado = await contexto.SaldosDeCierre
            .SingleAsync(s => s.CierreId == cierre.Id && s.ArticuloId == articulo.Id);

        // Lo que valia entonces sigue siendo 20 €, aunque hoy el almacen valga 110 €.
        Assert.Equal(Importe.De(20m), declarado.Valor);
    }

    [Fact]
    public async Task Un_movimiento_con_fecha_anterior_a_lo_ya_registrado_queda_marcado()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        var primera = await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-3));

        // Este llega tarde: su albaran es anterior a lo que ya estaba registrado.
        var traspapelado = await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-7),
            "albaran traspapelado");

        Assert.False(primera.Retroactivo);
        Assert.True(traspapelado.Retroactivo);
    }
}
