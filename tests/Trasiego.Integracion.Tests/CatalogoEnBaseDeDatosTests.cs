using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class CatalogoEnBaseDeDatosTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Un_articulo_dado_de_alta_se_recupera_por_su_referencia()
    {
        await using var contexto = baseDeDatos.Contexto();
        var repositorio = new RepositorioDeArticulos(contexto);

        await repositorio.Alta(new Articulo("TOR-M8-30", "Tornillo M8 30mm", UnidadDeMedida.Unidad));

        // En minusculas y con espacios, como lo teclearia cualquiera.
        var encontrado = await repositorio.PorReferencia(" tor-m8-30 ");

        Assert.NotNull(encontrado);
        Assert.Equal("Tornillo M8 30mm", encontrado.Nombre);
        Assert.Equal(UnidadDeMedida.Unidad, encontrado.Unidad);
    }

    [Fact]
    public async Task Dos_articulos_con_la_misma_referencia_no_caben()
    {
        await using var contexto = baseDeDatos.Contexto();
        var repositorio = new RepositorioDeArticulos(contexto);

        await repositorio.Alta(new Articulo("CAB-2X1", "Cable 2x1", UnidadDeMedida.Metro));

        await using var otroContexto = baseDeDatos.Contexto();
        var otroRepositorio = new RepositorioDeArticulos(otroContexto);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            otroRepositorio.Alta(new Articulo("CAB-2X1", "Cable 2x1 negro", UnidadDeMedida.Metro)));
    }

    [Fact]
    public async Task El_listado_normal_deja_fuera_lo_que_esta_de_baja()
    {
        await using var contexto = baseDeDatos.Contexto();
        var repositorio = new RepositorioDeAlmacenes(contexto);

        await repositorio.Alta(new Almacen("TDA", "Tienda"));

        var deBaja = new Almacen("OBR", "Obra terminada");
        deBaja.DarDeBaja();
        await repositorio.Alta(deBaja);

        var activos = await repositorio.Listar(incluirBajas: false);
        var todos = await repositorio.Listar(incluirBajas: true);

        Assert.DoesNotContain(activos, a => a.Codigo == "OBR");
        Assert.Contains(todos, a => a.Codigo == "OBR");
    }
}
