using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class DevolucionesYRegularizacionesTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Lo_devuelto_vuelve_al_coste_al_que_salio_y_no_al_de_hoy()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        // Diez a 1 €, se sacan cuatro, y despues entran diez a 8 €.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-5));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-3));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(80m), Escenario.Hoy.AddDays(-1));

        var devolucion = await servicio.DevolverSalida(
            salida.Id, Cantidad.De(4), Escenario.Hoy, "devuelve el taller");

        Assert.Equal(Importe.De(4m), salida.Coste);
        Assert.Equal(Importe.De(4m), devolucion.Coste);
        Assert.Equal(MotivoDeMovimiento.Devolucion, devolucion.Motivo);
        Assert.Equal(salida.Id, devolucion.MovimientoOrigenId);

        // 6 que quedaban + 10 que entraron + 4 que vuelven, y 6 + 80 + 4 en dinero.
        Assert.Equal(
            Importe.De(90m),
            await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task En_fifo_lo_devuelto_repone_la_capa_de_la_que_salio()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-5));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-3));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(80m), Escenario.Hoy.AddDays(-1));

        await servicio.DevolverSalida(salida.Id, Cantidad.De(4), Escenario.Hoy);

        var capas = await valoracion.CapasConExistencias(articulo.Id, almacen.Id);

        // La capa barata vuelve a estar entera; la cara sigue intacta.
        Assert.Equal([Cantidad.De(10), Cantidad.De(10)], capas.Select(c => c.CantidadRestante));
        Assert.Equal([Importe.De(10m), Importe.De(80m)], capas.Select(c => c.CosteRestante));
    }

    [Fact]
    public async Task A_precio_medio_lo_devuelto_entra_al_coste_original_pero_rehace_la_media()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(
            contexto, metodo: MetodoDeValoracion.PrecioMedio);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-5));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-3));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(80m), Escenario.Hoy.AddDays(-1));

        var devolucion = await servicio.DevolverSalida(salida.Id, Cantidad.De(4), Escenario.Hoy);

        // Vuelve a 1 € la unidad, que es lo que costo, no a la media de ese momento.
        Assert.Equal(Importe.De(4m), devolucion.Coste);

        // Pero al mezclarse cambia la media: 20 unidades por 90 € salen a 4,50.
        var siguiente = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(2), Escenario.Hoy);

        Assert.Equal(Importe.De(9m), siguiente.Coste);
    }

    [Fact]
    public async Task No_se_devuelve_mas_de_lo_que_salio()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-2));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(3), Escenario.Hoy);

        await servicio.DevolverSalida(salida.Id, Cantidad.De(2), Escenario.Hoy);

        await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.DevolverSalida(salida.Id, Cantidad.De(2), Escenario.Hoy));
    }

    [Fact]
    public async Task Solo_se_devuelve_lo_que_ha_salido()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        var entrada = await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.DevolverSalida(entrada.Id, Cantidad.De(1), Escenario.Hoy));

        Assert.Equal("Solo se devuelve lo que ha salido.", fallo.Message);
    }

    [Fact]
    public async Task Un_recuento_por_debajo_saca_la_diferencia_por_las_capas()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);

        // Diez a 2 €, pero el recuento dice ocho.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-1));

        var ajuste = await servicio.Regularizar(
            articulo.Id, almacen.Id, Cantidad.De(8), Escenario.Hoy, "recuento de marzo");

        Assert.NotNull(ajuste);
        Assert.Equal(TipoDeMovimiento.Salida, ajuste.Tipo);
        Assert.Equal(MotivoDeMovimiento.Regularizacion, ajuste.Motivo);
        Assert.Equal(Cantidad.De(2), ajuste.Cantidad);
        Assert.Equal(Importe.De(4m), ajuste.Coste);
        Assert.Equal(Cantidad.De(8), await movimientos.Saldo(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Un_recuento_por_encima_entra_al_precio_de_lo_que_ya_habia()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-1));

        var ajuste = await servicio.Regularizar(
            articulo.Id, almacen.Id, Cantidad.De(12), Escenario.Hoy);

        Assert.NotNull(ajuste);
        Assert.Equal(TipoDeMovimiento.Entrada, ajuste.Tipo);
        Assert.Equal(Importe.De(4m), ajuste.Coste);

        // Doce unidades por 24 €: lo aparecido vale lo mismo que lo que ya estaba, asi que
        // el precio unitario del almacen no se mueve.
        Assert.Equal(
            Importe.De(24m),
            await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Un_recuento_que_cuadra_no_genera_movimiento()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-1));

        Assert.Null(await servicio.Regularizar(
            articulo.Id, almacen.Id, Cantidad.De(10), Escenario.Hoy));
    }

    [Fact]
    public async Task Sin_existencias_no_hay_precio_al_que_regularizar_al_alza()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Servicio(contexto)
                .Regularizar(articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy));

        Assert.Contains("Registra la diferencia como una entrada normal", fallo.Message);
    }

    [Fact]
    public async Task La_invariante_aguanta_devoluciones_y_regularizaciones()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        // Numeros que no caen redondos, para que el redondeo tenga donde acumularse.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-6));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(7), Importe.De(23.33m), Escenario.Hoy.AddDays(-4));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-2));
        await Cuadra();

        await servicio.DevolverSalida(salida.Id, Cantidad.De(1), Escenario.Hoy.AddDays(-1));
        await Cuadra();

        await servicio.Regularizar(articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy);
        await Cuadra();

        await servicio.Regularizar(articulo.Id, almacen.Id, Cantidad.De(9), Escenario.Hoy);
        await Cuadra();

        async Task Cuadra() => Assert.Equal(
            await movimientos.CosteNeto(articulo.Id, almacen.Id),
            await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }
}
