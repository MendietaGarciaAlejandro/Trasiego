using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class RecalculoTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Reproducir_un_historico_en_orden_da_exactamente_lo_que_ya_habia()
    {
        // Este es el test que sujeta todo lo demas. El orquestado de la valoracion esta
        // escrito dos veces (una con persistencia en el servicio y otra sin ella en el
        // recalculo), y esto es lo que impide que las dos versiones se separen.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-9));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(7), Importe.De(23.33m), Escenario.Hoy.AddDays(-7));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-5));
        await servicio.DevolverSalida(salida.Id, Cantidad.De(1), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(2), Escenario.Hoy.AddDays(-3));
        await servicio.Regularizar(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-2));

        await Reproduce(contexto, articulo.Id, almacen.Id);
    }

    [Fact]
    public async Task Tambien_cuadra_valorando_a_precio_medio()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(
            contexto, metodo: MetodoDeValoracion.PrecioMedio);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-9));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(2), Escenario.Hoy.AddDays(-7));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(7), Importe.De(23.33m), Escenario.Hoy.AddDays(-5));
        await servicio.DevolverSalida(salida.Id, Cantidad.De(2), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy.AddDays(-2));

        await Reproduce(contexto, articulo.Id, almacen.Id);
    }

    [Fact]
    public async Task Tambien_cuadra_habiendo_servido_en_descubierto()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, permiteDescubierto: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-9));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(8), Escenario.Hoy.AddDays(-7));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(9), Importe.De(41.11m), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(3), Escenario.Hoy.AddDays(-2));

        await Reproduce(contexto, articulo.Id, almacen.Id);
    }

    [Fact]
    public async Task Un_albaran_traspapelado_deja_una_salida_valorada_de_mas()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        // Entra material caro y se consume.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(80m), Escenario.Hoy.AddDays(-5));
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-4));

        // Y despues aparece un albaran anterior, mucho mas barato. En FIFO tendria que haber
        // salido este primero.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(10m), Escenario.Hoy.AddDays(-8),
            "albaran traspapelado");

        var reproduccion = await Escenario.Recalculo(contexto).Comparar(articulo.Id, almacen.Id);

        var descuadre = Assert.Single(reproduccion.Descuadradas);
        Assert.Equal(salida.Id, descuadre.MovimientoId);
        Assert.Equal(Importe.De(32m), descuadre.Registrado);    // cuatro a 8 €
        Assert.Equal(Importe.De(4m), descuadre.Reproducido);    // habrian sido cuatro a 1 €
        Assert.Equal(Importe.De(-28m), descuadre.Diferencia);
    }

    [Fact]
    public async Task El_almacen_dice_que_articulos_conviene_mirar()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (tranquilo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        // Un segundo articulo en el mismo almacen, este con un albaran que llega tarde.
        var conRetraso = await Escenario.Catalogo(contexto);

        await servicio.RegistrarEntrada(
            tranquilo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-5));

        await servicio.RegistrarEntrada(
            conRetraso.Articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m),
            Escenario.Hoy.AddDays(-3));
        await servicio.RegistrarEntrada(
            conRetraso.Articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m),
            Escenario.Hoy.AddDays(-6));

        var sospechosos = await Escenario.Recalculo(contexto).ArticulosConRetroactivos(almacen.Id);

        Assert.Equal([conRetraso.Articulo.Id], sospechosos);
    }

    [Fact]
    public async Task Reproducir_arranca_desde_el_cierre_y_no_desde_el_principio()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-9));
        await Escenario.Cierres(contexto).Cerrar(almacen.Id, Escenario.Hoy.AddDays(-6));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(60m), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(12), Escenario.Hoy.AddDays(-2));

        await Reproduce(contexto, articulo.Id, almacen.Id);
    }

    /// <summary>
    /// Reproduce el historico y exige que no se aparte ni un centimo de lo registrado, ni en
    /// las salidas ni en el valor final del almacen.
    /// </summary>
    private static async Task Reproduce(ContextoDeTrasiego contexto, Guid articuloId, Guid almacenId)
    {
        var reproduccion = await Escenario.Recalculo(contexto).Comparar(articuloId, almacenId);

        Assert.Empty(reproduccion.Descuadradas);

        Assert.Equal(
            await new RepositorioDeMovimientos(contexto).SaldoDe(articuloId, almacenId),
            reproduccion.Cantidad);

        Assert.Equal(
            await new RepositorioDeValoracion(contexto).ValorDeLasExistencias(articuloId, almacenId),
            reproduccion.Valor);
    }
}
