using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

/// <summary>
/// Servir un lote concreto en vez de dejar que salga el que toque por caducidad. Es lo que
/// hace falta en una retirada de producto, o cuando el cliente exige el mismo lote que la vez
/// anterior.
/// </summary>
[Collection(nameof(ColeccionConBaseDeDatos))]
public class LotePedidoTests(BaseDeDatosDePruebas baseDeDatos)
{
    private static int _siguiente;

    [Fact]
    public async Task Se_sirve_el_lote_pedido_aunque_no_sea_el_que_tocaba()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m),
            Escenario.Hoy.AddDays(-5), lote: "L-PRONTO", caducidad: Escenario.Hoy.AddDays(10));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(50m),
            Escenario.Hoy.AddDays(-5), lote: "L-TARDE", caducidad: Escenario.Hoy.AddDays(90));

        // Por caducidad tocaria L-PRONTO, a 2 € la unidad. Se pide el otro, que va a 5 €.
        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy, lote: "L-TARDE");

        Assert.Equal(Importe.De(20m), salida.Coste);

        var quedan = await Lotes(contexto, almacen.Id);
        Assert.Equal(Cantidad.De(10), quedan.Single(c => c.Lote == "L-PRONTO").CantidadRestante);
        Assert.Equal(Cantidad.De(6), quedan.Single(c => c.Lote == "L-TARDE").CantidadRestante);
    }

    [Fact]
    public async Task El_lote_pedido_se_lee_como_se_guarda()
    {
        // Tecleado con espacios y en minusculas, como saldria de copiar una etiqueta. Si cada
        // sitio lo normalizara a su manera, este lote no aparecería.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m),
            Escenario.Hoy.AddDays(-1), lote: "L-2601");

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(2), Escenario.Hoy, lote: " l-2601 ");

        Assert.Equal(Importe.De(4m), salida.Coste);
    }

    [Fact]
    public async Task Pedir_un_lote_que_no_esta_lo_dice_con_su_nombre()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m),
            Escenario.Hoy.AddDays(-1), lote: "L-1");

        var noEsta = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(
                articulo.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy, lote: "L-9"));

        Assert.Contains("no queda nada del lote L-9", noEsta.Message);

        // Y si esta pero no llega, la queja dice de que lote habla y no del articulo entero.
        var noLlega = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(
                articulo.Id, almacen.Id, Cantidad.De(8), Escenario.Hoy, lote: "L-1"));

        Assert.Contains("del lote L-1", noLlega.Message);
    }

    [Fact]
    public async Task Pedir_por_su_nombre_un_lote_caducado_no_lo_hace_apto()
    {
        // Para sacar lo caducado esta el recuento, no el pedirlo con mas insistencia.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m),
            Escenario.Hoy.AddDays(-30), lote: "L-1", caducidad: Escenario.Hoy.AddDays(-1));

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(
                articulo.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy, lote: "L-1"));

        Assert.Contains("estan caducadas y no se sirven", fallo.Message);
    }

    [Fact]
    public async Task Un_lote_entero_se_puede_apartar_a_otro_almacen()
    {
        // La retirada de verdad: el lote sospechoso se manda a cuarentena y el almacen sigue
        // trabajando con el resto.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var cuarentena = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(6), Importe.De(12m),
            Escenario.Hoy.AddDays(-5), lote: "L-BUENO", caducidad: Escenario.Hoy.AddDays(10));

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(6), Importe.De(30m),
            Escenario.Hoy.AddDays(-5), lote: "L-MALO", caducidad: Escenario.Hoy.AddDays(90));

        var traspaso = await servicio.Traspasar(
            articulo.Id, origen.Id, cuarentena.Id, Cantidad.De(6), Escenario.Hoy,
            "retirada", lote: "L-MALO");

        Assert.Equal(Importe.De(30m), traspaso.Entrada.Coste);

        Assert.Equal("L-MALO", (await Lotes(contexto, cuarentena.Id)).Single().Lote);
        Assert.Equal("L-BUENO", (await Lotes(contexto, origen.Id)).Single().Lote);
    }

    [Fact]
    public async Task A_un_articulo_sin_lotes_no_se_le_puede_pedir_uno()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-1));

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(
                articulo.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy, lote: "L-1"));

        Assert.Contains("no se lleva por lotes", fallo.Message);
    }

    [Fact]
    public async Task Una_entrega_puede_decir_de_que_lote_sale()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var documentos = Escenario.Documentos(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m),
            Escenario.Hoy.AddDays(-5), lote: "L-A", caducidad: Escenario.Hoy.AddDays(10));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(25m),
            Escenario.Hoy.AddDays(-5), lote: "L-B", caducidad: Escenario.Hoy.AddDays(90));

        var entrega = await documentos.Abrir(
            TipoDeDocumento.Entrega, Numero(), almacen.Id, Escenario.Hoy);

        await documentos.AgregarLinea(
            entrega.Id, articulo.Id, Cantidad.De(2), Importe.Cero, lote: "L-B");

        var hechos = await servicio.RegistrarDocumento(entrega.Id);

        // Del caro, que es el que se pidio, y no del que caducaba antes.
        Assert.Equal(Importe.De(10m), hechos.Single().Coste);
    }

    [Fact]
    public async Task Lo_que_sale_no_declara_caducidad()
    {
        // La caducidad la trae lo que entra; al servir ya viene puesta con el lote.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var documentos = Escenario.Documentos(contexto);

        var entrega = await documentos.Abrir(
            TipoDeDocumento.Entrega, Numero(), almacen.Id, Escenario.Hoy);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            documentos.AgregarLinea(
                entrega.Id, articulo.Id, Cantidad.De(1), Importe.Cero,
                lote: "L-1", caducidad: Escenario.Hoy.AddDays(30)));

        Assert.Contains("Solo las recepciones dicen la caducidad", fallo.Message);
    }

    private static string Numero() => $"SAL-P{Interlocked.Increment(ref _siguiente)}";

    private static async Task<IReadOnlyList<CapaDeExistencias>> Lotes(
        ContextoDeTrasiego contexto,
        Guid almacenId) =>
        await new RepositorioDeValoracion(contexto).Lotes(almacenId);
}
