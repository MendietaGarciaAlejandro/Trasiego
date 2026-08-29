using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;

namespace Trasiego.Dominio.Tests;

public class CatalogoTests
{
    [Fact]
    public void La_referencia_se_guarda_normalizada()
    {
        var articulo = new Articulo("  tor-m8-30 ", "Tornillo M8 30mm", UnidadDeMedida.Unidad);

        Assert.Equal("TOR-M8-30", articulo.Referencia);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Un_articulo_sin_referencia_no_se_crea(string? referencia)
    {
        // ThrowsAny y no Throws: con null salta ArgumentNullException, que hereda de
        // ArgumentException, y xUnit exige el tipo exacto.
        Assert.ThrowsAny<ArgumentException>(
            () => new Articulo(referencia!, "Tornillo", UnidadDeMedida.Unidad));
    }

    [Fact]
    public void La_referencia_no_pasa_de_cuarenta_caracteres()
    {
        Assert.Throws<ArgumentException>(
            () => new Articulo(new string('X', 41), "Tornillo", UnidadDeMedida.Unidad));
    }

    [Fact]
    public void Un_articulo_no_se_da_de_baja_dos_veces()
    {
        var articulo = new Articulo("TOR-M8-30", "Tornillo M8 30mm", UnidadDeMedida.Unidad);
        articulo.DarDeBaja();

        var fallo = Assert.Throws<Conflicto>(articulo.DarDeBaja);
        Assert.Equal("El articulo TOR-M8-30 ya estaba de baja.", fallo.Message);
    }

    [Fact]
    public void El_codigo_de_almacen_se_guarda_normalizado()
    {
        Assert.Equal("CEN", new Almacen("cen", "Almacen central").Codigo);
    }

    [Fact]
    public void Todas_las_unidades_tienen_abreviatura()
    {
        foreach (var unidad in Enum.GetValues<UnidadDeMedida>())
        {
            Assert.False(string.IsNullOrWhiteSpace(unidad.Abreviatura()));
        }
    }
}
