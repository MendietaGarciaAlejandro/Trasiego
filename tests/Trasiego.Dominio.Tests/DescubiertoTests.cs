using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Tests;

public class DescubiertoTests
{
    [Fact]
    public void Taparlo_a_plazos_cancela_exactamente_lo_que_se_habia_apuntado()
    {
        // Tres unidades servidas sin tener, valoradas en 10,00 €.
        var descubierto = Descubierto(3, 10m);

        var cancelado = Importe.Cero;
        for (var i = 0; i < 3; i++) cancelado += descubierto.Cubrir(Cantidad.De(1));

        Assert.True(descubierto.Saldado);
        Assert.Equal(Importe.De(10m), cancelado);
        Assert.Equal(Importe.Cero, descubierto.CosteSinCubrir);
    }

    [Fact]
    public void No_se_tapa_mas_de_lo_que_se_debe()
    {
        var descubierto = Descubierto(3, 10m);

        Assert.Throws<Conflicto>(() => descubierto.Cubrir(Cantidad.De(4)));
    }

    [Fact]
    public void Un_saldo_en_descubierto_no_tiene_nada_disponible()
    {
        var saldo = Saldo.De(-5m);

        Assert.True(saldo.EnDescubierto);
        Assert.True(saldo.Disponible.EsCero);
    }

    [Fact]
    public void Un_saldo_normal_si_se_compara_con_cantidades()
    {
        var saldo = Saldo.De(10m);

        Assert.True(saldo > Cantidad.De(9));
        Assert.True(Cantidad.De(11) > saldo);
        Assert.True(saldo == Cantidad.De(10));
    }

    private static Descubierto Descubierto(decimal cantidad, decimal coste) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Cantidad.De(cantidad), Importe.De(coste));
}
