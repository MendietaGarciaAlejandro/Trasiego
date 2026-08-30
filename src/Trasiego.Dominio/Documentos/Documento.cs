using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Documentos;

public enum TipoDeDocumento
{
    /// <summary>Lo que llega: un albaran de entrada con sus lineas.</summary>
    Recepcion = 1,

    /// <summary>Lo que se sirve.</summary>
    Entrega = 2,

    /// <summary>Lo que cambia de almacen.</summary>
    Traspaso = 3,
}

public enum EstadoDeDocumento
{
    /// <summary>Se puede tocar y todavia no ha movido nada.</summary>
    Borrador = 1,

    /// <summary>Ya ha generado sus movimientos y no se toca.</summary>
    Registrado = 2,
}

/// <summary>
/// Varias lineas que llegaron juntas y se registran juntas.
/// </summary>
/// <remarks>
/// Esto no es una compra ni una venta: no hay proveedor, ni impuestos, ni precios de tarifa.
/// Es el papel que viene con la mercancia, con su numero y su fecha, para que un albaran de
/// doce lineas deje de ser doce movimientos sueltos con el numero escrito a mano en un campo
/// de texto.
/// </remarks>
public class Documento
{
    private readonly List<LineaDeDocumento> _lineas = [];

    private Documento() { }

    public Documento(
        TipoDeDocumento tipo,
        string numero,
        Guid almacenId,
        DateOnly fechaContable,
        Guid? almacenDestinoId = null,
        string? concepto = null)
    {
        if (tipo is TipoDeDocumento.Traspaso)
        {
            if (almacenDestinoId is not { } destino)
                throw new ReglaDeNegocio("Un traspaso necesita saber a que almacen va.");

            if (destino == almacenId)
                throw new ReglaDeNegocio("El origen y el destino son el mismo almacen.");
        }
        else if (almacenDestinoId is not null)
        {
            throw new ReglaDeNegocio("Solo un traspaso tiene almacen de destino.");
        }

        Id = Guid.CreateVersion7();
        Tipo = tipo;
        Numero = Comprobar.ComoMucho(Comprobar.NoEnBlanco(numero), 40).ToUpperInvariant();
        AlmacenId = almacenId;
        AlmacenDestinoId = almacenDestinoId;
        FechaContable = fechaContable;
        Concepto = string.IsNullOrWhiteSpace(concepto)
            ? null
            : Comprobar.ComoMucho(concepto.Trim(), 200);
    }

    public Guid Id { get; private set; }

    public TipoDeDocumento Tipo { get; private set; }

    public string Numero { get; private set; } = "";

    public Guid AlmacenId { get; private set; }

    /// <summary>Solo en los traspasos.</summary>
    public Guid? AlmacenDestinoId { get; private set; }

    /// <summary>
    /// La del papel, y la que llevaran todas sus lineas. Es lo que hace que un albaran sea
    /// una sola cosa y no doce movimientos que casualmente coinciden en fecha.
    /// </summary>
    public DateOnly FechaContable { get; private set; }

    public string? Concepto { get; private set; }

    public EstadoDeDocumento Estado { get; private set; } = EstadoDeDocumento.Borrador;

    public DateTimeOffset? MomentoDeRegistro { get; private set; }

    public IReadOnlyList<LineaDeDocumento> Lineas => _lineas;

    public bool EsBorrador => Estado is EstadoDeDocumento.Borrador;

    public LineaDeDocumento Agregar(
        Guid articuloId,
        Cantidad cantidad,
        Importe coste,
        string? lote = null,
        DateOnly? caducidad = null)
    {
        SoloEnBorrador();

        // En una recepcion el coste y el lote vienen del papel. En lo que sale no se teclean:
        // el coste lo pone la valoracion y el lote tambien, sacando primero lo que antes
        // caduque. Aceptarlos aqui daria a entender que sirven para algo.
        if (Tipo is TipoDeDocumento.Recepcion)
        {
            if (coste < Importe.Cero)
                throw new ReglaDeNegocio("Lo que cuesta una linea no puede ser negativo.");
        }
        else if (!coste.EsCero)
        {
            throw new ReglaDeNegocio(
                "Solo las recepciones llevan coste: en lo que sale lo pone la valoracion.");
        }
        else if (!string.IsNullOrWhiteSpace(lote) || caducidad is not null)
        {
            throw new ReglaDeNegocio(
                "Solo las recepciones dicen el lote: en lo que sale sale primero lo que antes caduca.");
        }

        var linea = new LineaDeDocumento(
            Id, _lineas.Count, articuloId, cantidad, coste, lote, caducidad);
        _lineas.Add(linea);

        return linea;
    }

    public void Quitar(Guid lineaId)
    {
        SoloEnBorrador();

        var linea = _lineas.SingleOrDefault(l => l.Id == lineaId)
            ?? throw new NoEncontrado("Esa linea no esta en el documento.");

        _lineas.Remove(linea);
    }

    /// <summary>
    /// Lo da por bueno. A partir de aqui no se toca: lo que haya que corregir se corrige con
    /// otro movimiento, que es como se corrigen las cosas en un almacen.
    /// </summary>
    public void DarPorRegistrado(DateTimeOffset momento)
    {
        SoloEnBorrador();

        if (_lineas.Count == 0)
            throw new ReglaDeNegocio("Un documento sin lineas no mueve nada.");

        Estado = EstadoDeDocumento.Registrado;
        MomentoDeRegistro = momento;
    }

    private void SoloEnBorrador()
    {
        if (!EsBorrador)
            throw new Conflicto($"El documento {Numero} ya esta registrado y no se toca.");
    }
}

public class LineaDeDocumento
{
    private LineaDeDocumento() { }

    public LineaDeDocumento(
        Guid documentoId,
        int orden,
        Guid articuloId,
        Cantidad cantidad,
        Importe coste,
        string? lote = null,
        DateOnly? caducidad = null)
    {
        Id = Guid.CreateVersion7();
        DocumentoId = documentoId;
        Orden = orden;
        ArticuloId = articuloId;
        Lote = string.IsNullOrWhiteSpace(lote)
            ? null
            : Comprobar.ComoMucho(lote.Trim(), 40).ToUpperInvariant();
        Caducidad = caducidad;
        Cantidad = cantidad.EsCero
            ? throw new ReglaDeNegocio("Una linea de cantidad cero no mueve nada.")
            : cantidad;
        Coste = coste;
    }

    public Guid Id { get; private set; }

    public Guid DocumentoId { get; private set; }

    /// <summary>El orden del papel, que es el orden en que se valoran.</summary>
    public int Orden { get; private set; }

    public Guid ArticuloId { get; private set; }

    public Cantidad Cantidad { get; private set; }

    /// <summary>Lo que costo esa linea entera. Cero en todo lo que no sea una recepcion.</summary>
    public Importe Coste { get; private set; }

    /// <summary>
    /// De que lote llega, en las recepciones de articulos que se llevan por lotes. Lo que
    /// sale no lo dice: eso lo decide la valoracion sacando primero lo que antes caduque.
    /// </summary>
    public string? Lote { get; private set; }

    public DateOnly? Caducidad { get; private set; }
}
