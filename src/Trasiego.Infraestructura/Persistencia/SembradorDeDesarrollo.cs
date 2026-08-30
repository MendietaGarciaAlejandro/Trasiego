using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Aplicacion.Cierres;
using Trasiego.Aplicacion.Documentos;
using Trasiego.Aplicacion.Movimientos;
using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;
using Trasiego.Infraestructura.Persistencia.Repositorios;

namespace Trasiego.Infraestructura.Persistencia;

/// <summary>
/// Deja la base de datos de desarrollo con un almacen que ya ha vivido un par de meses.
/// </summary>
/// <remarks>
/// <para>
/// Los usuarios hacen falta para poder entrar: sin ellos, la primera vez que se arranca no hay
/// forma de identificarse y tampoco de crear a nadie, porque dar de alta usuarios ya pide
/// estar identificado.
/// </para>
/// <para>
/// El historico hace falta por otra cosa. Casi todo lo que sabe hacer esto no se ve en una
/// pantalla vacia: hay que mirar un kardex con capas a dos precios, un traspaso, un almacen
/// que sirvio sin tener genero, y un lote caducado ocupando sitio. Tecleado a mano son diez
/// minutos que nadie va a dedicar antes de decidir si el proyecto le interesa.
/// </para>
/// <para>
/// Los movimientos se registran por los servicios de siempre y no metiendo filas a mano.
/// Sembrar por debajo seria facil y estaria mal: las capas, los consumos y los descubiertos
/// saldrian de lo que yo creo que tendrian que valer en vez de de lo que valen.
/// </para>
/// </remarks>
public static class SembradorDeDesarrollo
{
    public const string Contrasena = "trasiego-demo-2026";

    public static async Task Sembrar(
        ContextoDeTrasiego contexto,
        IHuellaDeContrasenas huellas,
        CancellationToken cancelacion = default)
    {
        if (await contexto.Usuarios.AnyAsync(cancelacion)) return;

        var encargada = new Usuario(
            "encargada@trasiego.test", "Encargada de almacen",
            huellas.Calcular(Contrasena), RolDeUsuario.Responsable);

        var operario = new Usuario(
            "operario@trasiego.test", "Operario de almacen",
            huellas.Calcular(Contrasena), RolDeUsuario.Operario);

        contexto.Usuarios.AddRange(encargada, operario);
        await contexto.SaveChangesAsync(cancelacion);

        await DosMesesDeAlmacen(contexto, encargada.Id, operario.Id, cancelacion);
    }

    private static async Task DosMesesDeAlmacen(
        ContextoDeTrasiego contexto,
        Guid encargada,
        Guid operario,
        CancellationToken cancelacion)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        DateOnly Dia(int haceCuantos) => hoy.AddDays(-haceCuantos);

        var quien = new Quien { Id = operario };
        var (movimientos, documentos, cierres) = Servicios(contexto, quien);

        var catalogo = new RepositorioDeArticulos(contexto);
        var almacenes = new RepositorioDeAlmacenes(contexto);

        // ---- El catalogo ----------------------------------------------------------------

        var central = new Almacen("CEN", "Almacen central");
        var obra = new Almacen("OBR", "Obra Ronda Sur", permiteDescubierto: true);
        var tienda = new Almacen("TDA", "Tienda");

        foreach (var almacen in new[] { central, obra, tienda })
            await almacenes.Alta(almacen, cancelacion);

        var tornillo = new Articulo("TOR-M8", "Tornillo M8 30 mm", UnidadDeMedida.Unidad);
        var cable = new Articulo(
            "CAB-25", "Cable 2,5 mm2", UnidadDeMedida.Metro, MetodoDeValoracion.PrecioMedio);
        var pintura = new Articulo("PIN-BLA", "Pintura plastica blanca 15 L", UnidadDeMedida.Unidad);
        var sellador = new Articulo(
            "SEL-PU", "Sellador de poliuretano 300 ml", UnidadDeMedida.Unidad,
            MetodoDeValoracion.Fifo, llevaLotes: true);

        foreach (var articulo in new[] { tornillo, cable, pintura, sellador })
            await catalogo.Alta(articulo, cancelacion);

        // ---- Lo que pasa en el almacen central ------------------------------------------
        //
        // Dos entradas de tornillos a precios distintos, para que el kardex enseñe dos capas
        // y una salida a caballo de las dos.

        await movimientos.RegistrarEntrada(
            tornillo.Id, central.Id, Cantidad.De(100), Importe.De(200m),
            Dia(60), "pedido inicial", cancelacion: cancelacion);

        await movimientos.RegistrarEntrada(
            cable.Id, central.Id, Cantidad.De(500), Importe.De(750m),
            Dia(58), "bobina grande", cancelacion: cancelacion);

        await movimientos.RegistrarEntrada(
            pintura.Id, central.Id, Cantidad.De(20), Importe.De(640m),
            Dia(55), "palet de pintura", cancelacion: cancelacion);

        await movimientos.RegistrarSalida(
            tornillo.Id, central.Id, Cantidad.De(30), Dia(50), "obra del puerto",
            cancelacion: cancelacion);

        // Segunda bobina mas cara. Como el cable va a precio medio, esto no abre capa: engorda
        // la que habia y rehace la media, que es lo que se espera de una media.
        await movimientos.RegistrarEntrada(
            cable.Id, central.Id, Cantidad.De(300), Importe.De(540m),
            Dia(45), "bobina de repuesto", cancelacion: cancelacion);

        // Los tornillos subieron. Esta si abre capa, y la anterior se sigue gastando primero.
        await movimientos.RegistrarEntrada(
            tornillo.Id, central.Id, Cantidad.De(100), Importe.De(260m),
            Dia(42), "reposicion, mas caros", cancelacion: cancelacion);

        // ---- Se cierra el central hasta hace cinco semanas -------------------------------

        quien.Id = encargada;

        await cierres.Cerrar(
            central.Id, Dia(35), "cierre mensual", cancelacion);

        quien.Id = operario;

        // ---- Lo de despues del cierre ---------------------------------------------------
        //
        // Una salida que se come lo que quedaba de la primera capa y sigue por la segunda.

        await movimientos.RegistrarSalida(
            tornillo.Id, central.Id, Cantidad.De(90), Dia(30), "obra Ronda Sur",
            cancelacion: cancelacion);

        var devuelta = await movimientos.RegistrarSalida(
            cable.Id, central.Id, Cantidad.De(120), Dia(28), "instalacion planta baja",
            cancelacion: cancelacion);

        // Sobro cable y vuelve. No entra al precio de hoy: vuelve al que salio.
        await movimientos.DevolverSalida(
            devuelta.Id, Cantidad.De(20), Dia(24), "sobrante de la instalacion", cancelacion);

        // ---- La obra, que sirve antes de que llegue el papel -----------------------------

        await movimientos.Traspasar(
            pintura.Id, central.Id, obra.Id, Cantidad.De(5), Dia(26),
            "reparto a la obra", cancelacion: cancelacion);

        // Ocho botes con cinco en el almacen: la obra queda debiendo tres, valorados al ultimo
        // precio que se conoce. Esto solo lo permite un almacen marcado para ello.
        await movimientos.RegistrarSalida(
            pintura.Id, obra.Id, Cantidad.De(8), Dia(20), "fachada norte",
            cancelacion: cancelacion);

        // Y aqui se tapa el agujero. Llego mas barata, y la diferencia la absorbe lo que queda.
        await movimientos.RegistrarEntrada(
            pintura.Id, obra.Id, Cantidad.De(10), Importe.De(300m), Dia(14),
            "compra directa en la obra", cancelacion: cancelacion);

        // ---- Un albaran de verdad, con sus dos lineas -----------------------------------

        var albaran = await documentos.Abrir(
            TipoDeDocumento.Recepcion, "ALB-2026-118", central.Id, Dia(12),
            concepto: "Ferreteria del Norte", cancelacion: cancelacion);

        await documentos.AgregarLinea(
            albaran.Id, tornillo.Id, Cantidad.De(50), Importe.De(140m),
            cancelacion: cancelacion);

        await documentos.AgregarLinea(
            albaran.Id, pintura.Id, Cantidad.De(6), Importe.De(198m),
            cancelacion: cancelacion);

        await movimientos.RegistrarDocumento(albaran.Id, cancelacion);

        // ---- El sellador de la tienda, que caduca ---------------------------------------
        //
        // Tres lotes con fechas escalonadas. El primero caduca esta semana, y de el queda algo
        // sin servir: eso es lo que aparece tachado en la pantalla de lotes.

        await movimientos.RegistrarEntrada(
            sellador.Id, tienda.Id, Cantidad.De(24), Importe.De(96m), Dia(40),
            "primer pedido", lote: "L-2601", caducidad: Dia(3), cancelacion: cancelacion);

        await movimientos.RegistrarEntrada(
            sellador.Id, tienda.Id, Cantidad.De(24), Importe.De(108m), Dia(30),
            "segundo pedido", lote: "L-2602", caducidad: hoy.AddDays(45),
            cancelacion: cancelacion);

        await movimientos.RegistrarEntrada(
            sellador.Id, tienda.Id, Cantidad.De(24), Importe.De(120m), Dia(18),
            "tercer pedido", lote: "L-2603", caducidad: hoy.AddDays(120),
            cancelacion: cancelacion);

        // Veinte botes. Sale del lote que antes caduca, que es el primero, aunque entonces
        // todavia le quedaba mes y medio de vida.
        await movimientos.RegistrarSalida(
            sellador.Id, tienda.Id, Cantidad.De(20), Dia(10), "reparto a distribuidores",
            cancelacion: cancelacion);

        // ---- Un recuento y un albaran traspapelado --------------------------------------

        quien.Id = encargada;

        // El inventario dice que hay un bote menos del que decia el sistema.
        await movimientos.Regularizar(
            pintura.Id, central.Id, Cantidad.De(20), Dia(5), "recuento de estanteria",
            cancelacion);

        // Y esto es lo que deja la marca de "tarde" en el kardex: llega hoy con fecha de hace
        // tres semanas, o sea despues de movimientos que ya estaban registrados.
        await movimientos.RegistrarEntrada(
            tornillo.Id, central.Id, Cantidad.De(25), Importe.De(70m), Dia(21),
            "albaran traspapelado", cancelacion: cancelacion);
    }

    private static (ServicioDeMovimientos, ServicioDeDocumentos, ServicioDeCierres) Servicios(
        ContextoDeTrasiego contexto,
        IQuienRegistra quien)
    {
        var articulos = new RepositorioDeArticulos(contexto);
        var almacenes = new RepositorioDeAlmacenes(contexto);
        var movimientos = new RepositorioDeMovimientos(contexto);
        var valoracion = new RepositorioDeValoracion(contexto);
        var cierres = new RepositorioDeCierres(contexto);
        var documentos = new RepositorioDeDocumentos(contexto);
        var usuarios = new RepositorioDeUsuarios(contexto);
        var trabajo = new UnidadDeTrabajo(contexto);

        return (
            new ServicioDeMovimientos(
                articulos, almacenes, movimientos, valoracion, cierres, documentos, usuarios,
                trabajo, quien, TimeProvider.System),
            new ServicioDeDocumentos(documentos, articulos, almacenes),
            new ServicioDeCierres(
                almacenes, cierres, movimientos, valoracion, trabajo, TimeProvider.System));
    }

    /// <summary>
    /// Quien esta registrando ahora mismo. En la Api sale del token; aqui no hay peticion
    /// ninguna, asi que se va cambiando a mano segun quien deberia haber hecho cada cosa: el
    /// dia a dia lo teclea el operario y los cierres y los recuentos la encargada.
    /// </summary>
    private sealed class Quien : IQuienRegistra
    {
        public Guid? Id { get; set; }
    }
}
