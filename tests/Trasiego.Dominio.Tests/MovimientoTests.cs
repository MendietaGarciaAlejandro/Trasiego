using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Tests;

public class MovimientoTests
{
    private static readonly DateOnly Ayer = new(2026, 3, 14);
    private static readonly DateTimeOffset Ahora = new(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Un_movimiento_de_cantidad_cero_no_se_registra()
    {
        Assert.Throws<ReglaDeNegocio>(() => new Movimiento(
            Guid.CreateVersion7(), Guid.CreateVersion7(), TipoDeMovimiento.Entrada,
            Cantidad.Cero, Ayer, Ahora));
    }

    [Fact]
    public void Un_concepto_en_blanco_se_guarda_como_nada()
    {
        var movimiento = new Movimiento(
            Guid.CreateVersion7(), Guid.CreateVersion7(), TipoDeMovimiento.Entrada,
            Cantidad.De(3), Ayer, Ahora, "   ");

        Assert.Null(movimiento.Concepto);
    }

    [Fact]
    public void La_fecha_contable_y_el_momento_de_registro_no_tienen_por_que_coincidir()
    {
        var movimiento = new Movimiento(
            Guid.CreateVersion7(), Guid.CreateVersion7(), TipoDeMovimiento.Entrada,
            Cantidad.De(3), Ayer, Ahora);

        Assert.Equal(Ayer, movimiento.FechaContable);
        Assert.Equal(Ahora, movimiento.MomentoDeRegistro);
    }

    [Fact]
    public void Lo_que_se_lleva_en_unidades_no_admite_decimales()
    {
        var tornillo = new Articulo("TOR-M8-30", "Tornillo M8 30mm", UnidadDeMedida.Unidad);

        var fallo = Assert.Throws<ReglaDeNegocio>(() => tornillo.ComprobarCantidad(Cantidad.De(2.5m)));

        // Por los extremos y no por la frase entera: la cantidad se formatea con la cultura
        // de la maquina, y con la separacion decimal inglesa el mensaje seria otro.
        Assert.StartsWith("TOR-M8-30 se lleva en unidades:", fallo.Message);
        Assert.EndsWith("no es una cantidad valida.", fallo.Message);
    }

    [Fact]
    public void Lo_que_se_lleva_en_kilos_si_admite_decimales()
    {
        var cemento = new Articulo("CEM-25", "Cemento gris", UnidadDeMedida.Kilogramo);

        cemento.ComprobarCantidad(Cantidad.De(2.5m));
    }
}
