using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Comun;

namespace Trasiego.Aplicacion.Almacenes;

public class ServicioDeAlmacenes(IRepositorioDeAlmacenes almacenes)
{
    public Task<IReadOnlyList<Almacen>> Listar(
        bool incluirBajas = false,
        CancellationToken cancelacion = default) =>
        almacenes.Listar(incluirBajas, cancelacion);

    public async Task<Almacen> PorId(Guid id, CancellationToken cancelacion = default) =>
        await almacenes.PorId(id, cancelacion)
        ?? throw new NoEncontrado("No existe el almacen.");

    public async Task<Almacen> Alta(
        string codigo,
        string nombre,
        bool permiteDescubierto = false,
        CancellationToken cancelacion = default)
    {
        var almacen = new Almacen(codigo, nombre, permiteDescubierto);

        if (await almacenes.PorCodigo(almacen.Codigo, cancelacion) is not null)
            throw new Conflicto($"Ya hay un almacen con el codigo {almacen.Codigo}.");

        await almacenes.Alta(almacen, cancelacion);
        return almacen;
    }

    public async Task DarDeBaja(Guid id, CancellationToken cancelacion = default)
    {
        var almacen = await PorId(id, cancelacion);
        almacen.DarDeBaja();

        await almacenes.GuardarCambios(cancelacion);
    }
}
