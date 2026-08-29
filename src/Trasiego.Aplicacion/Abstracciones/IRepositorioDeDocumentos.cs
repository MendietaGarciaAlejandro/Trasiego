using Trasiego.Dominio.Documentos;

namespace Trasiego.Aplicacion.Abstracciones;

public interface IRepositorioDeDocumentos
{
    void Agregar(Documento documento);

    /// <summary>El documento con sus lineas.</summary>
    Task<Documento?> PorId(Guid id, CancellationToken cancelacion = default);

    Task<Documento?> PorNumero(
        TipoDeDocumento tipo,
        string numero,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<Documento>> DeAlmacen(
        Guid almacenId,
        EstadoDeDocumento? estado = null,
        CancellationToken cancelacion = default);

    /// <summary>El numero de cada documento, para poder enseñarlo sin traerselos enteros.</summary>
    Task<IReadOnlyDictionary<Guid, string>> NumerosDe(
        IEnumerable<Guid> ids,
        CancellationToken cancelacion = default);

    Task GuardarCambios(CancellationToken cancelacion = default);
}
