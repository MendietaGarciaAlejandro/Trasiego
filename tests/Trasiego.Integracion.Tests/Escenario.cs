using Microsoft.Extensions.Time.Testing;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Aplicacion.Cierres;
using Trasiego.Aplicacion.Documentos;
using Trasiego.Aplicacion.Informes;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Aplicacion.Valoracion;
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

    /// <summary>
    /// Un reloj parado deja a todos los movimientos con el mismo momento de registro, y eso
    /// no pasa en la realidad: dos altas seguidas ocurren en instantes distintos. Con el
    /// reloj clavado los empates se resolvian por id, que es aleatorio, y habia sitios donde
    /// el orden importaba. Cada lectura avanza un segundo, que sigue dejando todo en el
    /// mismo dia contable.
    /// </summary>
    private static FakeTimeProvider Reloj() =>
        new(Ahora) { AutoAdvanceAmount = TimeSpan.FromSeconds(1) };

    public static ServicioDeCierres Cierres(ContextoDeTrasiego contexto) =>
        new(new RepositorioDeAlmacenes(contexto),
            new RepositorioDeCierres(contexto),
            new RepositorioDeMovimientos(contexto),
            new RepositorioDeValoracion(contexto),
            new UnidadDeTrabajo(contexto),
            Reloj());

    public static ServicioDeInformes Informes(ContextoDeTrasiego contexto) =>
        new(new RepositorioDeAlmacenes(contexto),
            new RepositorioDeArticulos(contexto),
            new RepositorioDeMovimientos(contexto));

    public static ServicioDeDocumentos Documentos(ContextoDeTrasiego contexto) =>
        new(new RepositorioDeDocumentos(contexto),
            new RepositorioDeArticulos(contexto),
            new RepositorioDeAlmacenes(contexto));

    public static ServicioDeRecalculo Recalculo(ContextoDeTrasiego contexto) =>
        new(new RepositorioDeArticulos(contexto),
            new RepositorioDeMovimientos(contexto),
            new RepositorioDeValoracion(contexto),
            new RepositorioDeCierres(contexto),
            new UnidadDeTrabajo(contexto));

    /// <summary>
    /// Casi ningun test tiene detras a nadie, porque lo que prueban son las reglas y no quien
    /// las dispara. Los que si lo necesitan pasan un usuario.
    /// </summary>
    public static ServicioDeMovimientos Servicio(
        ContextoDeTrasiego contexto,
        Guid? usuarioId = null) =>
        new(new RepositorioDeArticulos(contexto),
            new RepositorioDeAlmacenes(contexto),
            new RepositorioDeMovimientos(contexto),
            new RepositorioDeValoracion(contexto),
            new RepositorioDeCierres(contexto),
            new RepositorioDeDocumentos(contexto),
            new RepositorioDeUsuarios(contexto),
            new UnidadDeTrabajo(contexto),
            new QuienSea(usuarioId),
            Reloj());

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

    /// <summary>Un almacen suelto, para lo que necesita dos.</summary>
    public static async Task<Almacen> OtroAlmacen(ContextoDeTrasiego contexto)
    {
        var numero = Interlocked.Increment(ref _siguiente);
        var almacen = new Almacen($"D{numero}", $"Almacen de destino {numero}");

        await new RepositorioDeAlmacenes(contexto).Alta(almacen);
        return almacen;
    }
}

internal record QuienSea(Guid? Id) : IQuienRegistra;
