using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class AplicarRecalculoTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Cerrar_guarda_el_desglose_de_las_capas_y_no_solo_el_saldo()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(5m), Escenario.Hoy.AddDays(-12));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(50m), Escenario.Hoy.AddDays(-10));

        var cierre = await Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-8));

        var fotos = await contexto.FotosDeCapa
            .Where(f => f.CierreId == cierre.Id)
            .OrderBy(f => f.FechaContable)
            .ToListAsync();

        Assert.Equal([Importe.De(5m), Importe.De(50m)], fotos.Select(f => f.Coste));
    }

    [Fact]
    public async Task Reproducir_respeta_el_desglose_de_capas_del_cierre()
    {
        // Sin la foto, el arranque seria una sola capa de 10 unidades por 55 €, y esta salida
        // costaria 27,50 en vez de 5. Que el saldo cuadre no basta: en FIFO importa en
        // cuantas capas estaba repartido.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(5m), Escenario.Hoy.AddDays(-12));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(50m), Escenario.Hoy.AddDays(-10));

        await Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-8));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy.AddDays(-5));

        Assert.Equal(Importe.De(5m), salida.Coste);
        Assert.Empty((await Escenario.Recalculo(contexto).Comparar(articulo.Id, almacen.Id))
            .Descuadradas);
    }

    [Fact]
    public async Task No_se_cierra_debiendo_genero()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, permiteDescubierto: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-9));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(8), Escenario.Hoy.AddDays(-7));

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-5)));

        Assert.Contains("tapa los descubiertos antes de cerrar", fallo.Message);
    }

    [Fact]
    public async Task No_se_devuelve_una_salida_de_un_periodo_ya_cerrado()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-12));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-10));

        await Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-8));

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.DevolverSalida(salida.Id, Cantidad.De(2), Escenario.Hoy));

        Assert.Contains("ya esta cerrado", fallo.Message);
    }

    [Fact]
    public async Task Aplicar_corrige_la_salida_que_quedo_mal_valorada()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(80m), Escenario.Hoy.AddDays(-5));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-8),
            "albaran traspapelado");

        Assert.Equal(Importe.De(32m), salida.Coste);

        await Escenario.Recalculo(contexto).Aplicar(articulo.Id, almacen.Id);

        var corregida = await contexto.Movimientos.SingleAsync(m => m.Id == salida.Id);
        Assert.Equal(Importe.De(4m), corregida.Coste);
    }

    [Fact]
    public async Task Despues_de_aplicar_ya_no_queda_nada_que_corregir()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var recalculo = Escenario.Recalculo(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(80m), Escenario.Hoy.AddDays(-5));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-8));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(9), Escenario.Hoy.AddDays(-2));

        Assert.NotEmpty((await recalculo.Comparar(articulo.Id, almacen.Id)).Descuadradas);

        await recalculo.Aplicar(articulo.Id, almacen.Id);

        Assert.Empty((await recalculo.Comparar(articulo.Id, almacen.Id)).Descuadradas);
    }

    [Fact]
    public async Task La_invariante_sigue_cuadrando_despues_de_aplicar()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-5));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(2), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(7), Importe.De(23.33m), Escenario.Hoy.AddDays(-9));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy.AddDays(-2));

        await Escenario.Recalculo(contexto).Aplicar(articulo.Id, almacen.Id);

        Assert.Equal(
            await new RepositorioDeMovimientos(contexto).CosteNeto(articulo.Id, almacen.Id),
            await new RepositorioDeValoracion(contexto).ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Una_devolucion_tambien_se_recalcula()
    {
        // El coste de una devolucion no lo teclea nadie: sale de deshacer los consumos de la
        // salida original. Si el historico se recoloca, ese coste cambia igual que el de la
        // salida, y hasta ahora se quedaba con el viejo.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(80m), Escenario.Hoy.AddDays(-5));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-4));
        var devolucion = await servicio.DevolverSalida(
            salida.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-3));

        Assert.Equal(Importe.De(32m), devolucion.Coste);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-8));

        await Escenario.Recalculo(contexto).Aplicar(articulo.Id, almacen.Id);

        var vuelta = await contexto.Movimientos.SingleAsync(m => m.Id == devolucion.Id);
        Assert.Equal(Importe.De(4m), vuelta.Coste);

        Assert.Equal(
            await new RepositorioDeMovimientos(contexto).CosteNeto(articulo.Id, almacen.Id),
            await new RepositorioDeValoracion(contexto).ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Aplicar_no_toca_nada_por_debajo_del_cierre()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-12));
        var congelada = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(2), Escenario.Hoy.AddDays(-11));

        await Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-10));

        // Por encima del cierre, un albaran que llega tarde.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(50m), Escenario.Hoy.AddDays(-3));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(9), Escenario.Hoy.AddDays(-2));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(5m), Escenario.Hoy.AddDays(-6));

        await Escenario.Recalculo(contexto).Aplicar(articulo.Id, almacen.Id);

        var sigueIgual = await contexto.Movimientos.SingleAsync(m => m.Id == congelada.Id);
        Assert.Equal(Importe.De(4m), sigueIgual.Coste);
    }
}
