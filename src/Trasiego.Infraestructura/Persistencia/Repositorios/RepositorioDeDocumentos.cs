using Microsoft.EntityFrameworkCore;
using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Documentos;

namespace Trasiego.Infraestructura.Persistencia.Repositorios;

public class RepositorioDeDocumentos(ContextoDeTrasiego contexto) : IRepositorioDeDocumentos
{
    public void Agregar(Documento documento) => contexto.Documentos.Add(documento);

    public Task<Documento?> PorId(Guid id, CancellationToken cancelacion = default) =>
        contexto.Documentos.FirstOrDefaultAsync(d => d.Id == id, cancelacion);

    public Task<Documento?> PorNumero(
        TipoDeDocumento tipo,
        string numero,
        CancellationToken cancelacion = default)
    {
        var buscado = numero.Trim().ToUpperInvariant();
        return contexto.Documentos
            .FirstOrDefaultAsync(d => d.Tipo == tipo && d.Numero == buscado, cancelacion);
    }

    public async Task<IReadOnlyList<Documento>> DeAlmacen(
        Guid almacenId,
        EstadoDeDocumento? estado = null,
        CancellationToken cancelacion = default)
    {
        // El de origen o el de destino: un traspaso sale en los dos almacenes.
        var consulta = contexto.Documentos
            .Where(d => d.AlmacenId == almacenId || d.AlmacenDestinoId == almacenId);

        if (estado is { } cual) consulta = consulta.Where(d => d.Estado == cual);

        return await consulta
            .OrderByDescending(d => d.FechaContable)
            .ThenBy(d => d.Numero)
            .ToListAsync(cancelacion);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NumerosDe(
        IEnumerable<Guid> ids,
        CancellationToken cancelacion = default) =>
        await contexto.Documentos
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Numero, cancelacion);

    public Task GuardarCambios(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
