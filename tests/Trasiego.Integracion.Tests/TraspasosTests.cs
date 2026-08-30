using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class TraspasosTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Lo_que_sale_de_un_almacen_entra_en_el_otro_al_mismo_coste()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);

        // Diez a 1 € y diez a 9 €. Lo que se traspase sale a 1 €, no a la media.
        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(10), Importe.De(90m), Escenario.Hoy.AddDays(-2));

        var traspaso = await servicio.Traspasar(
            articulo.Id, origen.Id, destino.Id, Cantidad.De(6), Escenario.Hoy, "a la obra");

        Assert.Equal(Importe.De(6m), traspaso.Salida.Coste);
        Assert.Equal(traspaso.Salida.Coste, traspaso.Entrada.Coste);
    }

    [Fact]
    public async Task Mover_genero_de_sitio_no_cambia_lo_que_vale_en_conjunto()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        // Diez unidades por 33,33 €: el unitario no cae redondo a proposito.
        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(10), Importe.De(33.33m), Escenario.Hoy.AddDays(-2));

        var antes = await valoracion.ValorDeLasExistencias(articulo.Id, origen.Id);

        await servicio.Traspasar(articulo.Id, origen.Id, destino.Id, Cantidad.De(3), Escenario.Hoy);

        var despues = await valoracion.ValorDeLasExistencias(articulo.Id, origen.Id)
                    + await valoracion.ValorDeLasExistencias(articulo.Id, destino.Id);

        Assert.Equal(antes, despues);
    }

    [Fact]
    public async Task Las_dos_mitades_quedan_atadas()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-1));

        var traspaso = await servicio.Traspasar(
            articulo.Id, origen.Id, destino.Id, Cantidad.De(2), Escenario.Hoy);

        Assert.Equal(MotivoDeMovimiento.Traspaso, traspaso.Salida.Motivo);
        Assert.Equal(MotivoDeMovimiento.Traspaso, traspaso.Entrada.Motivo);
        Assert.Equal(traspaso.Salida.Id, traspaso.Entrada.MovimientoOrigenId);
        Assert.Equal(origen.Id, traspaso.Salida.AlmacenId);
        Assert.Equal(destino.Id, traspaso.Entrada.AlmacenId);
    }

    [Fact]
    public async Task No_se_traspasa_a_uno_mismo()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Servicio(contexto).Traspasar(
                articulo.Id, almacen.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy));

        Assert.Equal("El origen y el destino son el mismo almacen.", fallo.Message);
    }

    [Fact]
    public async Task Si_no_hay_bastante_en_el_origen_no_se_mueve_nada()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-1));

        await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.Traspasar(articulo.Id, origen.Id, destino.Id, Cantidad.De(9), Escenario.Hoy));

        // Ni ha salido de uno ni ha llegado al otro.
        Assert.Equal(Saldo.De(5), await movimientos.SaldoDe(articulo.Id, origen.Id));
        Assert.Equal(Saldo.Cero, await movimientos.SaldoDe(articulo.Id, destino.Id));
    }

    [Fact]
    public async Task No_se_traspasa_a_un_almacen_con_esa_fecha_ya_cerrada()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-6));

        // Se cierra el destino, no el origen.
        await Escenario.Cierres(contexto).Cerrar(destino.Id, Escenario.Hoy.AddDays(-2));

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.Traspasar(
                articulo.Id, origen.Id, destino.Id, Cantidad.De(2), Escenario.Hoy.AddDays(-4)));

        Assert.Contains("esta cerrado hasta el", fallo.Message);
    }

    [Fact]
    public async Task El_recalculo_arrastra_al_almacen_al_que_se_habia_traspasado()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);

        // Entra caro, se traspasan cuatro a 8 € la unidad.
        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(10), Importe.De(80m), Escenario.Hoy.AddDays(-5));
        var traspaso = await servicio.Traspasar(
            articulo.Id, origen.Id, destino.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-4));

        Assert.Equal(Importe.De(32m), traspaso.Entrada.Coste);

        // Y despues aparece un albaran anterior a 1 € la unidad.
        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-8));

        var resultado = await Escenario.Recalculo(contexto).Aplicar(articulo.Id, origen.Id);

        // Lo que salio del origen ahora cuesta 4 €, y eso es lo que tiene que valer en el
        // destino: mover genero de sitio no cambia lo que vale, tampoco al recalcular.
        Assert.Equal([destino.Id], resultado.OtrosAlmacenes);

        var enElDestino = await contexto.Movimientos.SingleAsync(m => m.Id == traspaso.Entrada.Id);
        Assert.Equal(Importe.De(4m), enElDestino.Coste);

        var valoracion = new RepositorioDeValoracion(contexto);
        Assert.Equal(
            Importe.De(4m),
            await valoracion.ValorDeLasExistencias(articulo.Id, destino.Id));
    }

    [Fact]
    public async Task La_invariante_cuadra_en_los_dos_almacenes_despues_de_arrastrar()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);

        // Numeros que no caen redondos, para que el redondeo tenga donde acumularse.
        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(7), Importe.De(23.33m), Escenario.Hoy.AddDays(-5));
        await servicio.Traspasar(
            articulo.Id, origen.Id, destino.Id, Cantidad.De(3), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarSalida(
            articulo.Id, destino.Id, Cantidad.De(1), Escenario.Hoy.AddDays(-3));
        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(4), Importe.De(10m), Escenario.Hoy.AddDays(-9));

        await Escenario.Recalculo(contexto).Aplicar(articulo.Id, origen.Id);

        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        foreach (var almacen in new[] { origen.Id, destino.Id })
            Assert.Equal(
                await movimientos.CosteNeto(articulo.Id, almacen),
                await valoracion.ValorDeLasExistencias(articulo.Id, almacen));
    }

}
