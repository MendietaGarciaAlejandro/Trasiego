using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class DocumentosTests(BaseDeDatosDePruebas baseDeDatos)
{
    private static int _siguiente;

    [Fact]
    public async Task Una_recepcion_de_varias_lineas_entra_de_una_vez()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (tornillo, almacen) = await Escenario.Catalogo(contexto);
        var cable = (await Escenario.Catalogo(contexto)).Articulo;
        var documentos = Escenario.Documentos(contexto);

        var albaran = await documentos.Abrir(
            TipoDeDocumento.Recepcion, Numero("ALB"), almacen.Id, Escenario.Hoy.AddDays(-1),
            concepto: "material de obra");

        await documentos.AgregarLinea(
            albaran.Id, tornillo.Id, Cantidad.De(10), Importe.De(20m));
        await documentos.AgregarLinea(
            albaran.Id, cable.Id, Cantidad.De(5), Importe.De(45m));

        var hechos = await Escenario.Servicio(contexto).RegistrarDocumento(albaran.Id);

        Assert.Equal(2, hechos.Count);
        Assert.All(hechos, movimiento => Assert.Equal(albaran.Id, movimiento.DocumentoId));

        var valoracion = new RepositorioDeValoracion(contexto);
        Assert.Equal(Importe.De(20m), await valoracion.ValorDeLasExistencias(tornillo.Id, almacen.Id));
        Assert.Equal(Importe.De(45m), await valoracion.ValorDeLasExistencias(cable.Id, almacen.Id));
    }

    [Fact]
    public async Task Si_una_linea_no_puede_ser_no_entra_ninguna()
    {
        // La mercancia llego junta: no tiene sentido que la segunda linea falle y la primera
        // se quede dentro.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var otro = (await Escenario.Catalogo(contexto)).Articulo;
        var documentos = Escenario.Documentos(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-2));

        var entrega = await documentos.Abrir(
            TipoDeDocumento.Entrega, Numero("SAL"), almacen.Id, Escenario.Hoy);

        await documentos.AgregarLinea(entrega.Id, articulo.Id, Cantidad.De(4), Importe.Cero);
        await documentos.AgregarLinea(entrega.Id, otro.Id, Cantidad.De(1), Importe.Cero);

        // Del segundo articulo no hay nada en ese almacen.
        await Assert.ThrowsAsync<ReglaDeNegocio>(() => servicio.RegistrarDocumento(entrega.Id));

        await using var otroContexto = baseDeDatos.Contexto();

        // Ni ha salido lo de la primera linea, ni el documento se ha dado por registrado.
        Assert.Equal(
            Saldo.De(10),
            await new RepositorioDeMovimientos(otroContexto).SaldoDe(articulo.Id, almacen.Id));

        var comoQuedo = await otroContexto.Documentos.SingleAsync(d => d.Id == entrega.Id);
        Assert.True(comoQuedo.EsBorrador);
    }

    [Fact]
    public async Task Un_documento_registrado_ya_no_se_toca()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var documentos = Escenario.Documentos(contexto);

        var albaran = await documentos.Abrir(
            TipoDeDocumento.Recepcion, Numero("ALB"), almacen.Id, Escenario.Hoy);
        await documentos.AgregarLinea(albaran.Id, articulo.Id, Cantidad.De(3), Importe.De(9m));

        await Escenario.Servicio(contexto).RegistrarDocumento(albaran.Id);

        var fallo = await Assert.ThrowsAsync<Conflicto>(() =>
            documentos.AgregarLinea(albaran.Id, articulo.Id, Cantidad.De(1), Importe.De(3m)));

        Assert.Contains("ya esta registrado y no se toca", fallo.Message);

        await Assert.ThrowsAsync<Conflicto>(() =>
            Escenario.Servicio(contexto).RegistrarDocumento(albaran.Id));
    }

    [Fact]
    public async Task Un_documento_sin_lineas_no_mueve_nada()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (_, almacen) = await Escenario.Catalogo(contexto);

        var vacio = await Escenario.Documentos(contexto).Abrir(
            TipoDeDocumento.Recepcion, Numero("ALB"), almacen.Id, Escenario.Hoy);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Servicio(contexto).RegistrarDocumento(vacio.Id));

        Assert.Equal("Un documento sin lineas no mueve nada.", fallo.Message);
    }

    [Fact]
    public async Task En_lo_que_sale_no_se_teclea_el_coste()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var documentos = Escenario.Documentos(contexto);

        var entrega = await documentos.Abrir(
            TipoDeDocumento.Entrega, Numero("SAL"), almacen.Id, Escenario.Hoy);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            documentos.AgregarLinea(entrega.Id, articulo.Id, Cantidad.De(1), Importe.De(5m)));

        Assert.Contains("lo pone la valoracion", fallo.Message);
    }

    [Fact]
    public async Task Un_traspaso_en_documento_mueve_las_dos_mitades_de_cada_linea()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, origen) = await Escenario.Catalogo(contexto);
        var destino = await OtroAlmacen(contexto);
        var documentos = Escenario.Documentos(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, origen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-1));

        var papel = await documentos.Abrir(
            TipoDeDocumento.Traspaso, Numero("TRA"), origen.Id, Escenario.Hoy,
            almacenDestinoId: destino.Id);

        await documentos.AgregarLinea(papel.Id, articulo.Id, Cantidad.De(4), Importe.Cero);

        var hechos = await servicio.RegistrarDocumento(papel.Id);

        Assert.Equal(2, hechos.Count);
        Assert.All(hechos, movimiento => Assert.Equal(papel.Id, movimiento.DocumentoId));

        var valoracion = new RepositorioDeValoracion(contexto);
        Assert.Equal(Importe.De(8m), await valoracion.ValorDeLasExistencias(articulo.Id, destino.Id));
        Assert.Equal(Importe.De(12m), await valoracion.ValorDeLasExistencias(articulo.Id, origen.Id));
    }

    [Fact]
    public async Task El_kardex_enseña_de_que_papel_salio_cada_linea()
    {
        // Es la gracia de todo esto: el kardex deja de enseñar un texto escrito a mano y
        // enseña el albaran de verdad.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var documentos = Escenario.Documentos(contexto);
        var servicio = Escenario.Servicio(contexto);

        var numero = Numero("ALB");
        var albaran = await documentos.Abrir(
            TipoDeDocumento.Recepcion, numero, almacen.Id, Escenario.Hoy.AddDays(-1));
        await documentos.AgregarLinea(albaran.Id, articulo.Id, Cantidad.De(4), Importe.De(12m));
        await servicio.RegistrarDocumento(albaran.Id);

        // Y uno suelto, que sigue valiendo: no todo viene con un papel detras.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(1), Importe.De(3m), Escenario.Hoy);

        var kardex = await servicio.Kardex(articulo.Id, almacen.Id);

        Assert.Equal(numero, kardex[0].Documento);
        Assert.Null(kardex[1].Documento);
    }

    [Fact]
    public async Task No_se_repite_el_numero_dentro_del_mismo_tipo()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (_, almacen) = await Escenario.Catalogo(contexto);
        var documentos = Escenario.Documentos(contexto);

        var numero = Numero("ALB");
        await documentos.Abrir(TipoDeDocumento.Recepcion, numero, almacen.Id, Escenario.Hoy);

        await Assert.ThrowsAsync<Conflicto>(() =>
            documentos.Abrir(TipoDeDocumento.Recepcion, numero, almacen.Id, Escenario.Hoy));

        // Pero el mismo numero en una entrega es otro papel distinto.
        var entrega = await documentos.Abrir(
            TipoDeDocumento.Entrega, numero, almacen.Id, Escenario.Hoy);

        Assert.Equal(numero, entrega.Numero);
    }

    [Fact]
    public async Task Un_traspaso_necesita_saber_a_donde_va()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (_, almacen) = await Escenario.Catalogo(contexto);

        var fallo = await Assert.ThrowsAsync<ReglaDeNegocio>(() =>
            Escenario.Documentos(contexto).Abrir(
                TipoDeDocumento.Traspaso, Numero("TRA"), almacen.Id, Escenario.Hoy));

        Assert.Equal("Un traspaso necesita saber a que almacen va.", fallo.Message);
    }

    private static string Numero(string prefijo) =>
        $"{prefijo}-{Interlocked.Increment(ref _siguiente)}";

    private static async Task<Almacen> OtroAlmacen(ContextoDeTrasiego contexto)
    {
        var numero = Interlocked.Increment(ref _siguiente);
        var almacen = new Almacen($"D{numero}", $"Almacen de destino {numero}");

        await new RepositorioDeAlmacenes(contexto).Alta(almacen);
        return almacen;
    }
}
