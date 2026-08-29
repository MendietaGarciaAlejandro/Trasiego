using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Tests;

public class PrecioMedioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Absorber_una_entrada_hace_la_media_de_las_dos()
    {
        // Diez a 1 € y diez a 3 €: veinte que valen 2 € cada una.
        var capa = Capa(10, 10m);
        capa.Absorber(Cantidad.De(10), Importe.De(30m));

        Assert.Equal(Cantidad.De(20), capa.CantidadRestante);
        Assert.Equal(Importe.De(40m), capa.CosteRestante);
        Assert.Equal(Importe.De(10m), capa.Consumir(Cantidad.De(5)));
    }

    [Fact]
    public void La_media_se_rehace_con_cada_entrada_y_no_se_arrastra()
    {
        var capa = Capa(10, 10m);

        capa.Consumir(Cantidad.De(5));                      // quedan 5 por 5 €
        capa.Absorber(Cantidad.De(5), Importe.De(45m));     // entran 5 a 9 €

        // Diez unidades por 50 €, no la media de 1 y 9.
        Assert.Equal(Importe.De(50m), capa.CosteRestante);
        Assert.Equal(Importe.De(5m), capa.Consumir(Cantidad.De(1)));
    }

    [Fact]
    public void Absorber_no_cambia_la_fecha_de_la_capa()
    {
        // La fecha marca cuando se abrio el ciclo de existencias, y ordena las capas. Si
        // cada entrada la moviera, el orden de consumo dependeria de la ultima compra.
        var capa = Capa(10, 10m);
        var fecha = capa.FechaContable;

        capa.Absorber(Cantidad.De(10), Importe.De(30m));

        Assert.Equal(fecha, capa.FechaContable);
    }

    [Fact]
    public void El_criterio_no_se_toca_cuando_ya_hay_historico()
    {
        var articulo = new Articulo("TOR-M8-30", "Tornillo", UnidadDeMedida.Unidad);

        var fallo = Assert.Throws<Conflicto>(
            () => articulo.CambiarMetodo(MetodoDeValoracion.PrecioMedio, tieneMovimientos: true));

        Assert.Contains("ya tiene movimientos", fallo.Message);
        Assert.Equal(MetodoDeValoracion.Fifo, articulo.Metodo);
    }

    [Fact]
    public void El_criterio_se_cambia_mientras_el_articulo_este_sin_estrenar()
    {
        var articulo = new Articulo("TOR-M8-30", "Tornillo", UnidadDeMedida.Unidad);

        articulo.CambiarMetodo(MetodoDeValoracion.PrecioMedio, tieneMovimientos: false);

        Assert.Equal(MetodoDeValoracion.PrecioMedio, articulo.Metodo);
    }

    private static CapaDeExistencias Capa(decimal cantidad, decimal coste) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Cantidad.De(cantidad), Importe.De(coste), new DateOnly(2026, 3, 1), Ahora);
}
