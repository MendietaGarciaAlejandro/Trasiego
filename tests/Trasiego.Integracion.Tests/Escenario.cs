using Microsoft.Extensions.Time.Testing;
using Trasiego.Aplicacion.Cierres;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Valoracion;
using Trasiego.Infraestructura.Persistencia;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Integracion.Tests;

internal static class Escenario
{
    public static readonly DateTimeOffset Ahora = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Hoy = new(2026, 3, 15);

    // La base de datos es la misma para toda la coleccion, asi que cada test se monta su
    // articulo y su almacen: con referencias fijas se pisarian unos a otros.
    private static int _siguiente;

    public static ServicioDeCierres Cierres(ContextoDeTrasiego contexto) =>
        new(new RepositorioDeAlmacenes(contexto),
            new RepositorioDeCierres(contexto),
            new UnidadDeTrabajo(contexto),
            new FakeTimeProvider(Ahora));

    public static ServicioDeMovimientos Servicio(ContextoDeTrasiego contexto) =>
        new(new RepositorioDeArticulos(contexto),
            new RepositorioDeAlmacenes(contexto),
            new RepositorioDeMovimientos(contexto),
            new RepositorioDeValoracion(contexto),
            new RepositorioDeCierres(contexto),
            new UnidadDeTrabajo(contexto),
            new FakeTimeProvider(Ahora));

    public static async Task<(Articulo Articulo, Almacen Almacen)> Catalogo(
        ContextoDeTrasiego contexto,
        UnidadDeMedida unidad = UnidadDeMedida.Unidad,
        MetodoDeValoracion metodo = MetodoDeValoracion.Fifo,
        bool permiteDescubierto = false)
    {
        var numero = Interlocked.Increment(ref _siguiente);

        var articulo = new Articulo($"ART-{numero}", $"Articulo {numero}", unidad, metodo);
        await new RepositorioDeArticulos(contexto).Alta(articulo);

        var almacen = new Almacen($"A{numero}", $"Almacen {numero}", permiteDescubierto);
        await new RepositorioDeAlmacenes(contexto).Alta(almacen);

        return (articulo, almacen);
    }
}
