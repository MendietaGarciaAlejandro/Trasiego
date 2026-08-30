using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class LotesTests(BaseDeDatosDePruebas baseDeDatos)
{
    private static int _siguiente;

    [Fact]
    public async Task Sale_antes_lo_que_antes_caduca_aunque_haya_llegado_despues()
    {
        // Es la diferencia entre FIFO y FEFO, y la razon de ser de toda la fase: lo que llego
        // primero deja de ser lo que hay que servir primero.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m),
            Escenario.Hoy.AddDays(-10), lote: "L-VIEJO",
            caducidad: Escenario.Hoy.AddDays(60));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(50m),
            Escenario.Hoy.AddDays(-2), lote: "L-NUEVO",
            caducidad: Escenario.Hoy.AddDays(10));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy);

        // Sale del que caduca en diez dias, que llego el ultimo y cuesta 5 € la unidad.
        Assert.Equal(Importe.De(20m), salida.Coste);

        var quedan = await Lotes(contexto, almacen.Id);
        Assert.Equal(Cantidad.De(6), quedan.Single(capa => capa.Lote == "L-NUEVO").CantidadRestante);
        Assert.Equal(Cantidad.De(10), quedan.Single(capa => capa.Lote == "L-VIEJO").CantidadRestante);
    }

    [Fact]
    public async Task Lo_que_no_caduca_espera_a_lo_que_si()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(5m),
            Escenario.Hoy.AddDays(-10), lote: "SIN-FECHA");

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(20m),
            Escenario.Hoy.AddDays(-5), lote: "CON-FECHA",
            caducidad: Escenario.Hoy.AddDays(30));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy);

        // No tiene sentido guardar algo con fecha para sacar antes algo que no la tiene.
        Assert.Equal(Importe.De(20m), salida.Coste);
    }

    [Fact]
    public async Task Lo_caducado_no_se_sirve_aunque_este_ahi()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m),
            Escenario.Hoy.AddDays(-30), lote: "L-1",
            caducidad: Escenario.Hoy.AddDays(-1));

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy));

        Assert.Contains("estan caducadas y no se sirven", fallo.Message);

        // Pero sigue estando y sigue valiendo dinero: el almacen todavia no ha perdido nada.
        var (saldo, valor) = await servicio.Existencias(articulo.Id, almacen.Id);
        Assert.Equal(Saldo.De(10), saldo);
        Assert.Equal(Importe.De(20m), valor);
    }

    [Fact]
    public async Task Se_mira_la_caducidad_al_dia_del_movimiento_y_no_a_hoy()
    {
        // Una salida con albaran de la semana pasada sale con lo que valia aquel dia.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m),
            Escenario.Hoy.AddDays(-30), lote: "L-1",
            caducidad: Escenario.Hoy.AddDays(-5));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy.AddDays(-10));

        Assert.Equal(Importe.De(8m), salida.Coste);
    }

    [Fact]
    public async Task Un_recuento_si_puede_llevarse_lo_caducado()
    {
        // Es la unica manera de darlo de baja: no se sirve, pero se cuenta y se tira.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(6), Importe.De(12m),
            Escenario.Hoy.AddDays(-30), lote: "CADUCADO",
            caducidad: Escenario.Hoy.AddDays(-1));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(4), Importe.De(20m),
            Escenario.Hoy.AddDays(-2), lote: "BUENO",
            caducidad: Escenario.Hoy.AddDays(90));

        // Se tiran las seis caducadas y se cuentan las cuatro que quedan.
        var merma = await servicio.Regularizar(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy, "material caducado");

        Assert.NotNull(merma);
        Assert.Equal(Importe.De(12m), merma.Coste);

        var quedan = await Lotes(contexto, almacen.Id);
        Assert.Equal("BUENO", quedan.Single().Lote);
    }

    [Fact]
    public async Task Un_traspaso_lleva_los_lotes_al_otro_almacen()
    {
        // Mover genero de sitio no le cambia el lote igual que no le cambia lo que vale.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var destino = await Escenario.OtroAlmacen(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(6), Importe.De(12m),
            Escenario.Hoy.AddDays(-5), lote: "L-1", caducidad: Escenario.Hoy.AddDays(10));

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(6), Importe.De(30m),
            Escenario.Hoy.AddDays(-5), lote: "L-2", caducidad: Escenario.Hoy.AddDays(20));

        // Ocho unidades: las seis de L-1, que caduca antes, y dos de L-2.
        var traspaso = await servicio.Traspasar(
            articulo.Id, origen.Id, destino.Id, Cantidad.De(8), Escenario.Hoy);

        Assert.Equal(Importe.De(22m), traspaso.Entrada.Coste);

        var alla = await Lotes(contexto, destino.Id);

        Assert.Equal(["L-1", "L-2"], alla.Select(capa => capa.Lote));
        Assert.Equal([Cantidad.De(6), Cantidad.De(2)], alla.Select(capa => capa.CantidadRestante));
        Assert.Equal([Importe.De(12m), Importe.De(10m)], alla.Select(capa => capa.CosteRestante));
        Assert.Equal(
            [Escenario.Hoy.AddDays(10), Escenario.Hoy.AddDays(20)],
            alla.Select(capa => capa.Caducidad));
    }

    [Fact]
    public async Task Una_devolucion_vuelve_a_su_lote()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(25m),
            Escenario.Hoy.AddDays(-5), lote: "L-CARO", caducidad: Escenario.Hoy.AddDays(10));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(5m),
            Escenario.Hoy.AddDays(-5), lote: "L-BARATO", caducidad: Escenario.Hoy.AddDays(90));

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy.AddDays(-1));

        var vuelta = await servicio.DevolverSalida(salida.Id, Cantidad.De(2), Escenario.Hoy);

        // Salio del caro porque caduca antes, y vuelve al caro y a su precio.
        Assert.Equal(Importe.De(10m), vuelta.Coste);

        var quedan = await Lotes(contexto, almacen.Id);
        Assert.Equal(Cantidad.De(2), quedan.Single(capa => capa.Lote == "L-CARO").CantidadRestante);
    }

    [Fact]
    public async Task Un_articulo_con_lotes_no_sale_sin_estar()
    {
        // No se sirve un lote que no se tiene: no habria numero que poner. Y eso es ademas lo
        // que hace que sus costes registrados sean los definitivos.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(
            contexto, permiteDescubierto: true, llevaLotes: true);

        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(2), Importe.De(4m),
            Escenario.Hoy.AddDays(-1), lote: "L-1");

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarSalida(articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy));

        Assert.Contains("no sale sin estar", fallo.Message);
    }

    [Fact]
    public async Task Una_entrada_con_lotes_tiene_que_decir_de_cual_es()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (conLotes, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var sinLotes = (await Escenario.Catalogo(contexto)).Articulo;
        var servicio = Escenario.Servicio(contexto);

        var falta = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarEntrada(
                conLotes.Id, almacen.Id, Cantidad.De(1), Importe.De(2m), Escenario.Hoy));

        Assert.Contains("falta decir de cual es", falta.Message);

        var sobra = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.RegistrarEntrada(
                sinLotes.Id, almacen.Id, Cantidad.De(1), Importe.De(2m), Escenario.Hoy,
                lote: "L-1"));

        Assert.Contains("no se lleva por lotes", sobra.Message);
    }

    [Fact]
    public async Task No_entra_algo_que_ya_venia_caducado()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Servicio(contexto).RegistrarEntrada(
                articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(2m), Escenario.Hoy,
                lote: "L-1", caducidad: Escenario.Hoy.AddDays(-1)));

        Assert.Contains("entraria ya caducado", fallo.Message);
    }

    [Fact]
    public async Task Un_recuento_que_encuentra_de_mas_no_sabe_de_que_lote_es()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(4), Importe.De(8m),
            Escenario.Hoy.AddDays(-1), lote: "L-1");

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            servicio.Regularizar(articulo.Id, almacen.Id, Cantidad.De(6), Escenario.Hoy));

        Assert.Contains("no hay manera de saber de cual son", fallo.Message);
    }

    [Fact]
    public async Task Un_albaran_de_recepcion_trae_su_lote()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var documentos = Escenario.Documentos(contexto);

        var albaran = await documentos.Abrir(
            TipoDeDocumento.Recepcion, $"ALB-L{Interlocked.Increment(ref _siguiente)}",
            almacen.Id, Escenario.Hoy);

        await documentos.AgregarLinea(
            albaran.Id, articulo.Id, Cantidad.De(3), Importe.De(9m),
            lote: "L-DEL-PAPEL", caducidad: Escenario.Hoy.AddDays(45));

        await Escenario.Servicio(contexto).RegistrarDocumento(albaran.Id);

        var capa = (await Lotes(contexto, almacen.Id)).Single();

        Assert.Equal("L-DEL-PAPEL", capa.Lote);
        Assert.Equal(Escenario.Hoy.AddDays(45), capa.Caducidad);
    }

    [Fact]
    public async Task El_informe_dice_lo_que_caduca_antes_de_una_fecha()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(6m),
            Escenario.Hoy.AddDays(-1), lote: "PRONTO", caducidad: Escenario.Hoy.AddDays(5));

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(6m),
            Escenario.Hoy.AddDays(-1), lote: "TARDE", caducidad: Escenario.Hoy.AddDays(200));

        var informes = Escenario.Informes(contexto);

        var todo = await informes.Lotes(almacen.Id);
        Assert.Equal(["PRONTO", "TARDE"], todo.Select(linea => linea.Lote));

        var urgente = await informes.Lotes(almacen.Id, Escenario.Hoy.AddDays(30));
        Assert.Equal("PRONTO", urgente.Single().Lote);
    }

    [Fact]
    public async Task El_informe_no_saca_los_articulos_que_no_llevan_lotes()
    {
        // Los demas tambien tienen capas, pero ahi una capa es un detalle de como se valora:
        // enseñar tres filas sin numero de lote de un articulo que no los lleva es ruido.
        await using var contexto = baseDeDatos.Contexto();
        var (conLotes, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);
        var sinLotes = (await Escenario.Catalogo(contexto)).Articulo;
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            conLotes.Id, almacen.Id, Cantidad.De(2), Importe.De(4m),
            Escenario.Hoy.AddDays(-1), lote: "L-1");

        await servicio.RegistrarEntrada(
            sinLotes.Id, almacen.Id, Cantidad.De(9), Importe.De(18m), Escenario.Hoy);

        var lineas = await Escenario.Informes(contexto).Lotes(almacen.Id);

        Assert.Equal(conLotes.Id, lineas.Single().ArticuloId);
    }

    [Fact]
    public async Task Un_articulo_con_lotes_no_se_recalcula_porque_no_hay_nada_que_recalcular()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto, llevaLotes: true);

        await Escenario.Servicio(contexto).RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(2), Importe.De(4m),
            Escenario.Hoy.AddDays(-1), lote: "L-1");

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Recalculo(contexto).Comparar(articulo.Id, almacen.Id));

        Assert.Contains("no hay nada que recalcular", fallo.Message);
    }

    private static async Task<IReadOnlyList<CapaDeExistencias>> Lotes(
        ContextoDeTrasiego contexto,
        Guid almacenId) =>
        await new RepositorioDeValoracion(contexto).Lotes(almacenId);
}
