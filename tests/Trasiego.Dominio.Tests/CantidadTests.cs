using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Tests;

public class CantidadTests
{
    [Fact]
    public void No_existe_una_cantidad_negativa()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Cantidad.De(-1m));
    }

    [Fact]
    public void Se_guarda_con_cuatro_decimales()
    {
        Assert.Equal(1.2346m, Cantidad.De(1.23456m).Valor);
    }

    [Fact]
    public void Restar_mas_de_lo_que_hay_revienta_en_vez_de_dar_negativo()
    {
        var hay = Cantidad.De(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => hay - Cantidad.De(6));
    }

    [Fact]
    public void Restar_todo_deja_la_cantidad_a_cero()
    {
        Assert.True((Cantidad.De(5) - Cantidad.De(5)).EsCero);
    }

    [Fact]
    public void Se_comparan_por_valor()
    {
        Assert.Equal(Cantidad.De(2.50m), Cantidad.De(2.5m));
        Assert.True(Cantidad.De(3) > Cantidad.De(2.9999m));
    }
}
