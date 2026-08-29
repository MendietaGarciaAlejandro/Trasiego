using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Tests;

public class DevolucionesTests
{
    [Fact]
    public void Lo_que_vuelve_lo_hace_al_coste_que_tuvo_cada_consumo()
    {
        // La salida se llevo 10 de una capa a 2 € y 5 de otra a 3 €.
        var deLaBarata = Consumo(10, 20m);
        var deLaCara = Consumo(5, 15m);

        var vueltas = Devoluciones.Repartir([deLaBarata, deLaCara], Cantidad.De(12));

        Assert.Equal([Cantidad.De(10), Cantidad.De(2)], vueltas.Select(v => v.Cantidad));
        Assert.Equal([Importe.De(20m), Importe.De(6m)], vueltas.Select(v => v.Coste));
    }

    [Fact]
    public void Devolver_a_plazos_suma_exactamente_lo_que_costo()
    {
        // Tres unidades por 10,00 €, devueltas de una en una.
        var consumo = Consumo(3, 10m);

        var vuelto = Importe.Cero;
        for (var i = 0; i < 3; i++)
            vuelto += Devoluciones.Repartir([consumo], Cantidad.De(1)).Single().Coste;

        Assert.Equal(Importe.De(10m), vuelto);
        Assert.Equal(Importe.De(10m), consumo.CosteDevuelto);
        Assert.True(consumo.SinDevolver.EsCero);
    }

    [Fact]
    public void No_se_devuelve_mas_de_lo_que_salio()
    {
        var consumo = Consumo(5, 10m);

        var fallo = Assert.Throws<ReglaDeNegocio>(
            () => Devoluciones.Repartir([consumo], Cantidad.De(6)));

        Assert.Contains("sobran 1", fallo.Message);
    }

    [Fact]
    public void Un_consumo_ya_devuelto_del_todo_no_estorba()
    {
        var agotado = Consumo(5, 10m);
        Devoluciones.Repartir([agotado], Cantidad.De(5));

        var otro = Consumo(5, 25m);
        var vueltas = Devoluciones.Repartir([agotado, otro], Cantidad.De(2));

        Assert.Equal(otro.CapaId, vueltas.Single().CapaId);
    }

    private static ConsumoDeCapa Consumo(decimal cantidad, decimal coste) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Cantidad.De(cantidad), Importe.De(coste));
}
