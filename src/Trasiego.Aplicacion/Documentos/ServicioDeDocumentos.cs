using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Documentos;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Documentos;

/// <summary>
/// Se ocupa de los documentos mientras son borrador. Registrarlos, que es lo que mueve
/// mercancia de verdad, lo hace el servicio de movimientos, que es donde estan las piezas
/// para valorar.
/// </summary>
public class ServicioDeDocumentos(
    IRepositorioDeDocumentos documentos,
    IRepositorioDeArticulos articulos,
    IRepositorioDeAlmacenes almacenes)
{
    public async Task<Documento> Abrir(
        TipoDeDocumento tipo,
        string numero,
        Guid almacenId,
        DateOnly fechaContable,
        Guid? almacenDestinoId = null,
        string? concepto = null,
        CancellationToken cancelacion = default)
    {
        _ = await almacenes.PorId(almacenId, cancelacion)
            ?? throw new NoEncontrado("No existe el almacen.");

        if (almacenDestinoId is { } destino)
            _ = await almacenes.PorId(destino, cancelacion)
                ?? throw new NoEncontrado("No existe el almacen de destino.");

        var documento = new Documento(
            tipo, numero, almacenId, fechaContable, almacenDestinoId, concepto);

        if (await documentos.PorNumero(tipo, documento.Numero, cancelacion) is not null)
            throw new Conflicto($"Ya hay un documento {documento.Numero} de ese tipo.");

        documentos.Agregar(documento);
        await documentos.GuardarCambios(cancelacion);

        return documento;
    }

    public async Task<Documento> AgregarLinea(
        Guid documentoId,
        Guid articuloId,
        Cantidad cantidad,
        Importe coste,
        string? lote = null,
        DateOnly? caducidad = null,
        CancellationToken cancelacion = default)
    {
        var documento = await PorId(documentoId, cancelacion);

        var articulo = await articulos.PorId(articuloId, cancelacion)
            ?? throw new NoEncontrado("No existe el articulo.");

        if (!articulo.Activo)
            throw new ReglaDeNegocio($"El articulo {articulo.Referencia} esta de baja.");

        articulo.ComprobarCantidad(cantidad);

        // Solo en las recepciones: en lo que sale el documento ya se niega a llevar lote, y
        // exigirselo aqui a una entrega seria pedir un dato que no se puede dar.
        if (documento.Tipo is TipoDeDocumento.Recepcion) articulo.ComprobarLote(lote);

        documento.Agregar(articuloId, cantidad, coste, lote, caducidad);
        await documentos.GuardarCambios(cancelacion);

        return documento;
    }

    public async Task<Documento> QuitarLinea(
        Guid documentoId,
        Guid lineaId,
        CancellationToken cancelacion = default)
    {
        var documento = await PorId(documentoId, cancelacion);

        documento.Quitar(lineaId);
        await documentos.GuardarCambios(cancelacion);

        return documento;
    }

    public async Task<Documento> PorId(Guid id, CancellationToken cancelacion = default) =>
        await documentos.PorId(id, cancelacion)
        ?? throw new NoEncontrado("No existe ese documento.");

    public Task<IReadOnlyList<Documento>> DeAlmacen(
        Guid almacenId,
        EstadoDeDocumento? estado = null,
        CancellationToken cancelacion = default) =>
        documentos.DeAlmacen(almacenId, estado, cancelacion);
}
