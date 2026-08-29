using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

[Collection(nameof(ColeccionConBaseDeDatos))]
public class ConcurrenciaTests(BaseDeDatosDePruebas baseDeDatos)
{
    [Fact]
    public async Task Diez_salidas_a_la_vez_no_gastan_mas_de_lo_que_hay()
    {
        Guid articuloId, almacenId;

        await using (var preparando = baseDeDatos.Contexto())
        {
            var (articulo, almacen) = await Escenario.Catalogo(preparando);
            articuloId = articulo.Id;
            almacenId = almacen.Id;

            await Escenario.Servicio(preparando).RegistrarEntrada(
                articuloId, almacenId, Cantidad.De(5), Importe.De(10m),
                Escenario.Hoy.AddDays(-1));
        }

        // Cada intento con su propio contexto, que es lo que pasaria con diez peticiones
        // distintas llegando a la vez.
        var resultados = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var contexto = baseDeDatos.Contexto();
            try
            {
                await Escenario.Servicio(contexto).RegistrarSalida(
                    articuloId, almacenId, Cantidad.De(1), Escenario.Hoy);
                return true;
            }
            catch (ReglaDeNegocio)
            {
                return false;
            }
        }));

        Assert.Equal(5, resultados.Count(entro => entro));

        await using var comprobando = baseDeDatos.Contexto();
        Assert.Equal(
            Saldo.Cero,
            await new RepositorioDeMovimientos(comprobando).SaldoDe(articuloId, almacenId));
    }

    [Fact]
    public async Task Cuando_hay_para_todos_entran_todas_y_el_almacen_queda_a_cero()
    {
        Guid articuloId, almacenId;

        await using (var preparando = baseDeDatos.Contexto())
        {
            var (articulo, almacen) = await Escenario.Catalogo(preparando);
            articuloId = articulo.Id;
            almacenId = almacen.Id;

            // Diez unidades por 10,00 €: el unitario no cae redondo a proposito.
            await Escenario.Servicio(preparando).RegistrarEntrada(
                articuloId, almacenId, Cantidad.De(10), Importe.De(10m),
                Escenario.Hoy.AddDays(-1));
        }

        await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var contexto = baseDeDatos.Contexto();
            await Escenario.Servicio(contexto).RegistrarSalida(
                articuloId, almacenId, Cantidad.De(1), Escenario.Hoy);
        }));

        await using var comprobando = baseDeDatos.Contexto();
        var movimientos = new RepositorioDeMovimientos(comprobando);
        var valoracion = new RepositorioDeValoracion(comprobando);

        Assert.Equal(Saldo.Cero, await movimientos.SaldoDe(articuloId, almacenId));
        Assert.Equal(
            Importe.Cero,
            await valoracion.ValorDeLasExistencias(articuloId, almacenId));
        Assert.Equal(
            await movimientos.CosteNeto(articuloId, almacenId),
            await valoracion.ValorDeLasExistencias(articuloId, almacenId));
    }

    [Fact]
    public async Task Varias_entradas_a_la_vez_no_se_pisan_al_engordar_la_misma_capa()
    {
        // A precio medio todas las entradas van a la misma capa, asi que se pisan siempre:
        // es el caso que mas castiga el reintento.
        Guid articuloId, almacenId;

        await using (var preparando = baseDeDatos.Contexto())
        {
            var (articulo, almacen) = await Escenario.Catalogo(
                preparando, metodo: MetodoDeValoracion.PrecioMedio);
            articuloId = articulo.Id;
            almacenId = almacen.Id;

            await Escenario.Servicio(preparando).RegistrarEntrada(
                articuloId, almacenId, Cantidad.De(1), Importe.De(2m),
                Escenario.Hoy.AddDays(-1));
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var contexto = baseDeDatos.Contexto();
            await Escenario.Servicio(contexto).RegistrarEntrada(
                articuloId, almacenId, Cantidad.De(5), Importe.De(10m), Escenario.Hoy);
        }));

        await using var comprobando = baseDeDatos.Contexto();
        var valoracion = new RepositorioDeValoracion(comprobando);

        var capas = await valoracion.CapasConExistencias(articuloId, almacenId);

        Assert.Single(capas);
        Assert.Equal(Cantidad.De(41), capas[0].CantidadRestante);
        Assert.Equal(Importe.De(82m), capas[0].CosteRestante);
    }

    [Fact]
    public async Task Salidas_simultaneas_de_capas_distintas_se_valoran_cada_una_a_lo_suyo()
    {
        Guid articuloId, almacenId;

        await using (var preparando = baseDeDatos.Contexto())
        {
            var (articulo, almacen) = await Escenario.Catalogo(preparando);
            articuloId = articulo.Id;
            almacenId = almacen.Id;

            var servicio = Escenario.Servicio(preparando);

            // Cinco a 1 € y cinco a 9 €.
            await servicio.RegistrarEntrada(
                articuloId, almacenId, Cantidad.De(5), Importe.De(5m), Escenario.Hoy.AddDays(-4));
            await servicio.RegistrarEntrada(
                articuloId, almacenId, Cantidad.De(5), Importe.De(45m), Escenario.Hoy.AddDays(-2));
        }

        var costes = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var contexto = baseDeDatos.Contexto();
            var salida = await Escenario.Servicio(contexto).RegistrarSalida(
                articuloId, almacenId, Cantidad.De(1), Escenario.Hoy);
            return salida.Coste;
        }));

        // Cinco tienen que haber salido a 1 € y cinco a 9 €, en el orden en que se peleen.
        Assert.Equal(5, costes.Count(coste => coste == Importe.De(1m)));
        Assert.Equal(5, costes.Count(coste => coste == Importe.De(9m)));

        await using var comprobando = baseDeDatos.Contexto();
        Assert.Equal(
            Importe.Cero,
            await new RepositorioDeValoracion(comprobando)
                .ValorDeLasExistencias(articuloId, almacenId));
    }
}
