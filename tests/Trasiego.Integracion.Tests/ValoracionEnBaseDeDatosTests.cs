using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class ValoracionEnBaseDeDatosTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task El_coste_de_una_salida_no_lo_teclea_nadie_sale_de_las_capas()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        // Diez a 2 € y despues diez a 3 €.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-2));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(30m), Escenario.Hoy);

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(15), Escenario.Hoy);

        // Las diez primeras a 2 € y cinco de las siguientes a 3 €.
        Assert.Equal(Importe.De(35m), salida.Coste);
    }

    [Fact]
    public async Task Cada_salida_deja_dicho_de_que_capa_salio_cada_trozo()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-2));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(30m), Escenario.Hoy);

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(15), Escenario.Hoy);

        var consumos = await contexto.Consumos
            .Where(c => c.MovimientoId == salida.Id)
            .OrderBy(c => c.Coste)
            .ToListAsync();

        Assert.Equal([Cantidad.De(5), Cantidad.De(10)], consumos.Select(c => c.Cantidad));
        Assert.Equal([Importe.De(15m), Importe.De(20m)], consumos.Select(c => c.Coste));
    }

    [Fact]
    public async Task El_valor_de_lo_que_queda_cuadra_siempre_con_lo_que_entro_menos_lo_que_salio()
    {
        // La invariante del proyecto, con numeros que no caen redondos aposta: tres
        // unidades por 10,00 € salen a 3,333333... cada una.
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(10m), Escenario.Hoy.AddDays(-4));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(7), Importe.De(23.33m), Escenario.Hoy.AddDays(-1));

        // Se saca de una en una, que es donde el redondeo tendria ocasion de acumularse.
        for (var sacadas = 0; sacadas < 10; sacadas++)
        {
            await servicio.RegistrarSalida(
                articulo.Id, almacen.Id, Cantidad.De(1), Escenario.Hoy);

            var enLasCapas = await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id);
            var segunLosMovimientos = await movimientos.CosteNeto(articulo.Id, almacen.Id);

            Assert.Equal(segunLosMovimientos, enLasCapas);
        }

        Assert.Equal(Saldo.Cero, await movimientos.SaldoDe(articulo.Id, almacen.Id));
        Assert.Equal(Importe.Cero, await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Una_entrada_retroactiva_se_consume_antes_que_una_ya_registrada()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        // Primero se teclea la del dia 12, a 5 € la unidad.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(4), Importe.De(20m), Escenario.Hoy.AddDays(-3));

        // Y despues aparece un albaran del dia 8, a 1 € la unidad.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(4), Importe.De(4m), Escenario.Hoy.AddDays(-7),
            "albaran traspapelado");

        var salida = await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(4), Escenario.Hoy);

        // En FIFO manda la fecha del albaran, no el orden en que se tecleo.
        Assert.Equal(Importe.De(4m), salida.Coste);
    }
}
