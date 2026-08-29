using Trasiego.Aplicacion.Catalogo;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class PrecioMedioEnBaseDeDatosTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Los_mismos_movimientos_valen_distinto_segun_el_criterio_del_articulo()
    {
        // Diez a 1 € y despues diez a 3 €, y se sacan diez. Con FIFO salen las diez primeras
        // y cuestan 10 €; a precio medio las veinte valen 2 € cada una y cuestan 20 €.
        await using var contexto = baseDeDatos.Contexto();
        var servicio = Escenario.Servicio(contexto);

        var porFifo = await Escenario.Catalogo(contexto);
        var porMedia = await Escenario.Catalogo(
            contexto, metodo: MetodoDeValoracion.PrecioMedio);

        foreach (var (articulo, almacen) in new[] { porFifo, porMedia })
        {
            await servicio.RegistrarEntrada(
                articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m),
                Escenario.Hoy.AddDays(-2));
            await servicio.RegistrarEntrada(
                articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(30m), Escenario.Hoy);
        }

        var conFifo = await servicio.RegistrarSalida(
            porFifo.Articulo.Id, porFifo.Almacen.Id, Cantidad.De(10), Escenario.Hoy);
        var conMedia = await servicio.RegistrarSalida(
            porMedia.Articulo.Id, porMedia.Almacen.Id, Cantidad.De(10), Escenario.Hoy);

        Assert.Equal(Importe.De(10m), conFifo.Coste);
        Assert.Equal(Importe.De(20m), conMedia.Coste);
    }

    [Fact]
    public async Task A_precio_medio_solo_queda_una_capa_abierta_por_muchas_entradas_que_haya()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(
            contexto, metodo: MetodoDeValoracion.PrecioMedio);
        var servicio = Escenario.Servicio(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        for (var i = 1; i <= 4; i++)
            await servicio.RegistrarEntrada(
                articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(5m * i), Escenario.Hoy);

        var capas = await valoracion.CapasConExistencias(articulo.Id, almacen.Id);

        Assert.Single(capas);
        Assert.Equal(Cantidad.De(20), capas[0].CantidadRestante);
        Assert.Equal(Importe.De(50m), capas[0].CosteRestante);
    }

    [Fact]
    public async Task La_invariante_tambien_cuadra_valorando_a_precio_medio()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(
            contexto, metodo: MetodoDeValoracion.PrecioMedio);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        // Diez unidades por 33,33 €: la media no cae redonda a proposito.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(7), Importe.De(23.33m), Escenario.Hoy);

        for (var sacadas = 0; sacadas < 10; sacadas++)
        {
            await servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy);

            Assert.Equal(
                await movimientos.CosteNeto(articulo.Id, almacen.Id),
                await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
        }

        Assert.Equal(Importe.Cero, await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task El_criterio_no_se_cambia_una_vez_el_articulo_tiene_movimientos()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var articulos = new ServicioDeArticulos(
            new RepositorioDeArticulos(contexto), new RepositorioDeMovimientos(contexto));

        // Sin estrenar se puede.
        await articulos.CambiarMetodoDeValoracion(articulo.Id, MetodoDeValoracion.PrecioMedio);

        await Escenario.Servicio(contexto).RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(2m), Escenario.Hoy);

        await Assert.ThrowsAsync<Conflicto>(() =>
            articulos.CambiarMetodoDeValoracion(articulo.Id, MetodoDeValoracion.Fifo));
    }
}
