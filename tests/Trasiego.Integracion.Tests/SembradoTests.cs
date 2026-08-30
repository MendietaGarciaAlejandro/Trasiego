using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;
using Trasiego.Infraestructura.Seguridad;

namespace Trasiego.Integracion.Tests;

/// <summary>
/// El sembrado de desarrollo es lo primero que ve alguien que clona el repo, y ademas pasa por
/// casi todo el motor de valoracion: capas a dos precios, precio medio, un traspaso, un
/// descubierto tapado, lotes con caducidades y un cierre. Si se degrada, se degrada en
/// silencio, porque nadie mira una pantalla de demostracion con una calculadora al lado.
/// </summary>
/// <remarks>
/// Con base de datos propia y no la de la coleccion: el sembrador solo actua si no hay ningun
/// usuario, y la compartida los tiene desde que arranca la primera prueba de la Api.
/// </remarks>
public class SembradoTests : IAsyncLifetime
{
    private const string Instancia = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;";

    private readonly string _nombre = "trasiego_sembrado_" + Guid.NewGuid().ToString("N")[..12];

    private string CadenaDeConexion => $"{Instancia}Database={_nombre};";

    private ContextoDeTrasiego Contexto() =>
        new(new DbContextOptionsBuilder<ContextoDeTrasiego>()
            .UseSqlServer(CadenaDeConexion)
            .Options);

    public async Task InitializeAsync()
    {
        await using var contexto = Contexto();
        await contexto.Database.MigrateAsync();

        await SembradorDeDesarrollo.Sembrar(contexto, new HuellaBCrypt());
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var contexto = Contexto();
        await contexto.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Lo_sembrado_cumple_la_invariante_en_todas_partes()
    {
        // La unica regla que no se puede romper, comprobada sobre un historico de dos meses
        // que ha pasado por casi todo lo que sabe hacer esto.
        await using var contexto = Contexto();

        var articulos = await new RepositorioDeArticulos(contexto).Listar(true);
        var almacenes = await new RepositorioDeAlmacenes(contexto).Listar(true);
        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);

        foreach (var articulo in articulos)
            foreach (var almacen in almacenes)
                Assert.Equal(
                    await movimientos.CosteNeto(articulo.Id, almacen.Id),
                    await valoracion.ValorDeLasExistencias(articulo.Id, almacen.Id));
    }

    [Fact]
    public async Task Lo_sembrado_enseña_lo_que_tiene_que_enseñar()
    {
        // Cada una de estas es una pantalla que se queda sosa si el sembrado se queda corto.
        await using var contexto = Contexto();

        Assert.True(
            await contexto.Movimientos.AnyAsync(m => m.Retroactivo),
            "sin un albaran traspapelado, el kardex no enseña la marca de 'tarde'");

        Assert.True(
            await contexto.Descubiertos.AnyAsync(),
            "sin un almacen que sirvio sin tener genero, no se ve el saldo en negativo");

        Assert.True(
            await contexto.Descubiertos.AllAsync(d => d.CantidadCubierta == d.Cantidad),
            "el descubierto tiene que acabar tapado, que es donde se ve como se absorbe la diferencia");

        Assert.True(
            await contexto.Documentos.AnyAsync(),
            "sin un albaran registrado, la pantalla de documentos sale vacia");

        Assert.True(
            await contexto.Cierres.AnyAsync(),
            "sin un cierre, la pantalla de cierres sale vacia");

        var hoy = DateOnly.FromDateTime(DateTime.Now);

        Assert.True(
            await contexto.Capas.AnyAsync(c =>
                c.CantidadRestante != Cantidad.Cero && c.Caducidad != null && c.Caducidad < hoy),
            "sin un lote caducado con existencias, la pantalla de lotes no enseña lo que aporta");

        Assert.True(
            await contexto.Movimientos.AnyAsync(m => m.UsuarioId != null),
            "sin firma, la columna de quien registro cada cosa sale entera con rayas");
    }

    [Fact]
    public async Task Los_tornillos_llegaron_a_dos_precios_y_salieron_a_los_dos()
    {
        // Es el ejemplo con el que se explica FIFO en el README: una salida que se come lo que
        // quedaba de la primera capa y sigue por la segunda, mas cara.
        await using var contexto = Contexto();

        var tornillo = await contexto.Articulos.SingleAsync(a => a.Referencia == "TOR-M8");
        var central = await contexto.Almacenes.SingleAsync(a => a.Codigo == "CEN");

        var capas = await new RepositorioDeValoracion(contexto)
            .CapasConExistencias(tornillo.Id, central.Id);

        var entradas = await contexto.Movimientos
            .Where(m => m.ArticuloId == tornillo.Id && m.AlmacenId == central.Id)
            .Where(m => m.Tipo == Dominio.Movimientos.TipoDeMovimiento.Entrada)
            .CountAsync();

        Assert.True(entradas >= 3, "hacen falta varias entradas para que haya varias capas");
        Assert.True(capas.Count >= 2, "con una sola capa no se ve la diferencia entre criterios");

        // Y con costes distintos, que si no da igual de cual salga.
        Assert.True(
            capas.Select(capa => capa.CosteInicial.PorUnidad(capa.CantidadInicial)).Distinct().Count() > 1,
            "las capas tienen que estar a precios distintos");
    }
}
