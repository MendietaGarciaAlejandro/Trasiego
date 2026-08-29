using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Tests;

public class ValoracionFifoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Una_salida_que_cabe_en_la_primera_capa_no_toca_las_demas()
    {
        var primera = Capa(10, 20m, dia: 1);
        var segunda = Capa(10, 30m, dia: 5);

        var tomas = ValoracionFifo.Consumir([primera, segunda], Cantidad.De(4));

        Assert.Single(tomas);
        Assert.Equal(Importe.De(8m), tomas[0].Coste);
        Assert.Equal(Cantidad.De(10), segunda.CantidadRestante);
    }

    [Fact]
    public void Una_salida_a_caballo_de_dos_capas_coge_el_coste_de_cada_una()
    {
        // Diez a 2 € y diez a 3 €. Quince unidades cuestan 20 + 15.
        var primera = Capa(10, 20m, dia: 1);
        var segunda = Capa(10, 30m, dia: 5);

        var tomas = ValoracionFifo.Consumir([primera, segunda], Cantidad.De(15));

        Assert.Equal([Importe.De(20m), Importe.De(15m)], tomas.Select(t => t.Coste));
        Assert.True(primera.Agotada);
        Assert.Equal(Cantidad.De(5), segunda.CantidadRestante);
    }

    [Fact]
    public void Manda_la_fecha_contable_y_no_la_de_registro()
    {
        // La segunda se tecleo antes, pero su albaran es posterior.
        var albaranViejo = Capa(5, 10m, dia: 1, registro: Ahora);
        var albaranNuevo = Capa(5, 50m, dia: 9, registro: Ahora.AddDays(-3));

        var tomas = ValoracionFifo.Consumir([albaranNuevo, albaranViejo], Cantidad.De(5));

        Assert.Equal(albaranViejo.Id, tomas.Single().CapaId);
    }

    [Fact]
    public void Vaciar_una_capa_a_trozos_no_deja_ni_un_decimo_dentro()
    {
        // Tres unidades por 10,00 €: el coste unitario no cae redondo a proposito.
        var capa = Capa(3, 10m, dia: 1);

        var salido = Importe.Cero;
        for (var i = 0; i < 3; i++)
            salido += ValoracionFifo.Consumir([capa], Cantidad.De(1)).Single().Coste;

        Assert.True(capa.Agotada);
        Assert.Equal(Importe.Cero, capa.CosteRestante);
        Assert.Equal(Importe.De(10m), salido);
    }

    [Fact]
    public void Si_las_capas_no_llegan_se_para_en_vez_de_valorar_a_medias()
    {
        var capa = Capa(5, 10m, dia: 1);

        var fallo = Assert.Throws<Conflicto>(
            () => ValoracionFifo.Consumir([capa], Cantidad.De(8)));

        Assert.Contains("faltan 3", fallo.Message);
    }

    [Fact]
    public void Una_capa_ya_agotada_no_estorba()
    {
        var vacia = Capa(5, 10m, dia: 1);
        ValoracionFifo.Consumir([vacia], Cantidad.De(5));

        var buena = Capa(5, 25m, dia: 5);
        var tomas = ValoracionFifo.Consumir([vacia, buena], Cantidad.De(2));

        Assert.Equal(buena.Id, tomas.Single().CapaId);
    }

    private static CapaDeExistencias Capa(
        decimal cantidad,
        decimal coste,
        int dia,
        DateTimeOffset? registro = null) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Cantidad.De(cantidad), Importe.De(coste),
            new DateOnly(2026, 3, dia), registro ?? Ahora);
}
