using Trasiego.Dominio.Valores;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class InformesTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task La_valoracion_a_una_fecha_no_cuenta_lo_que_vino_despues()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(20m), Escenario.Hoy.AddDays(-8));
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(10), Importe.De(90m), Escenario.Hoy);

        var informes = Escenario.Informes(contexto);

        var entonces = await informes.ValoracionA(almacen.Id, Escenario.Hoy.AddDays(-5));
        var ahora = await informes.ValoracionA(almacen.Id, Escenario.Hoy);

        Assert.Equal(Importe.De(20m), entonces.Single().Valor);
        Assert.Equal(Importe.De(110m), ahora.Single().Valor);
    }

    [Fact]
    public async Task Lo_que_quedo_a_cero_de_todo_no_sale_en_el_informe()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);
        var otro = await Escenario.Catalogo(contexto);
        var servicio = Escenario.Servicio(contexto);

        // El primero entra y sale entero; el segundo se queda con existencias.
        await servicio.RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(5), Importe.De(10m), Escenario.Hoy.AddDays(-2));
        await servicio.RegistrarSalida(
            articulo.Id, almacen.Id, Cantidad.De(5), Escenario.Hoy);

        await servicio.RegistrarEntrada(
            otro.Articulo.Id, almacen.Id, Cantidad.De(3), Importe.De(9m), Escenario.Hoy);

        var lineas = await Escenario.Informes(contexto).ValoracionA(almacen.Id, Escenario.Hoy);

        Assert.Equal(otro.Articulo.Id, lineas.Single().ArticuloId);
    }

    [Fact]
    public async Task El_informe_dice_la_referencia_y_el_nombre_del_articulo()
    {
        await using var contexto = baseDeDatos.Contexto();
        var (articulo, almacen) = await Escenario.Catalogo(contexto);

        await Escenario.Servicio(contexto).RegistrarEntrada(
            articulo.Id, almacen.Id, Cantidad.De(4), Importe.De(12m), Escenario.Hoy);

        var linea = (await Escenario.Informes(contexto)
            .ValoracionA(almacen.Id, Escenario.Hoy)).Single();

        Assert.Equal(articulo.Referencia, linea.Referencia);
        Assert.Equal(articulo.Nombre, linea.Nombre);
        Assert.Equal(Saldo.De(4), linea.Cantidad);
    }

    [Fact]
    public async Task Un_almacen_que_no_existe_no_tiene_informe()
    {
        await using var contexto = baseDeDatos.Contexto();

        await Assert.ThrowsAsync<Trasiego.Dominio.Comun.NoEncontrado>(() =>
            Escenario.Informes(contexto).ValoracionA(Guid.CreateVersion7(), Escenario.Hoy));
    }
}
