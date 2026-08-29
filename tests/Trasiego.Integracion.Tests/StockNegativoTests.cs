using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class StockNegativoTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Un_almacen_normal_sigue_sin_dejar_servir_lo_que_no_tiene()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-1));

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(11), Escenario.Hoy));

        Assert.Contains("quedan 10 ud", fallo.Message);
    }

    [Fact]
    public async Task Lo_que_sale_sin_estar_se_valora_al_ultimo_precio_que_se_conoce()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, permiteDescubierto: true);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        // Diez a 2 €, y se sirven quince.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-1));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(15), Escenario.Hoy, "la obra no espera");

        // Diez de las capas a 2 € y cinco al ultimo precio conocido, que es el mismo.
        Assert.Equal(Importe.De(30m), salida.Coste);
        Assert.Equal(Saldo.De(-5), await movimientos.SaldoDe(articulo.Id, almacen.Id));

        // El almacen vale menos que nada: debe cinco unidades que valen 10 €.
        Assert.Equal(
            Importe.De(-10m),
            await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Si_nunca_ha_pasado_por_el_almacen_no_hay_precio_con_el_que_valorarlo()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, permiteDescubierto: true);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Servicio(contexto)
                .RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(3), Escenario.Hoy));

        Assert.Contains("no hay ningun precio", fallo.Message);
    }

    [Fact]
    public async Task La_entrada_que_llega_tapa_primero_el_descubierto()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, permiteDescubierto: true);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-2));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(15), Escenario.Hoy.AddDays(-1));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(8), Importe.De(32m), Escenario.Hoy);

        var descubiertos = await contexto.Descubiertos
            .Where(d => d.ArticuloId == articulo.Id)
            .ToListAsync();

        Assert.True(descubiertos.Single().Saldado);
        Assert.Equal(Saldo.De(3), await movimientos.SaldoDe(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task La_diferencia_entre_lo_estimado_y_lo_que_costo_la_absorbe_lo_que_queda()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, permiteDescubierto: true);
        var servicio = Escenario.Servicio(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        // Cinco unidades salieron valoradas a 2 €, que era lo ultimo que se sabia.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-2));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(15), Escenario.Hoy.AddDays(-1));

        // Pero el genero llego a 4 €.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(8), Importe.De(32m), Escenario.Hoy);

        // Quedan tres unidades que valen 22 € y no 12: los 10 € que se dejaron de contar en
        // aquella salida los carga lo que queda en el almacen. Lo ya valorado no se revisa.
        Assert.Equal(
            Importe.De(22m),
            await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Si_la_entrada_tapa_justo_el_descubierto_la_diferencia_se_queda_sin_donde_apoyarse()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, permiteDescubierto: true);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-2));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(15), Escenario.Hoy.AddDays(-1));

        // Cinco justas, pero a 5 € en vez de a los 2 € con los que se valoro la salida.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(25m), Escenario.Hoy);

        // El almacen se queda sin nada y valiendo 15 €. No es un fallo: es la diferencia de
        // coste de lo que se sirvio sin tener, que sigue siendo real aunque ya no queden
        // existencias que la carguen. Un sistema con contabilidad la llevaria a una cuenta
        // de resultados; aqui se queda a la vista.
        Assert.Equal(Saldo.Cero, await movimientos.SaldoDe(articulo.Id, almacen.Id));
        Assert.Equal(
            Importe.De(15m),
            await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task La_invariante_aguanta_todo_el_recorrido_del_descubierto()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, permiteDescubierto: true);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-5));
        await Cuadra();

        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(7), Escenario.Hoy.AddDays(-4));
        await Cuadra();

        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(2), Escenario.Hoy.AddDays(-3));
        await Cuadra();

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(4), Importe.De(23.33m), Escenario.Hoy.AddDays(-2));
        await Cuadra();

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(9), Importe.De(41.11m), Escenario.Hoy);
        await Cuadra();

        Assert.Equal(Saldo.De(7), await movimientos.SaldoDe(articulo.Id, almacen.Id));

        async Task Cuadra() => Assert.Equal(
            await movimientos.CosteNeto(articulo.Id, almacen.Id),
            await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }
}
