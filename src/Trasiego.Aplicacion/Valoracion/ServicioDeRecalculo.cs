using Trasiego.Aplicacion.Abstracciones;
using Trasiego.Dominio.Comun;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Aplicacion.Valoracion;

public class ServicioDeRecalculo(
    IRepositorioDeArticulos articulos,
    IRepositorioDeMovimientos movimientos,
    IRepositorioDeCierres cierres)
{
    /// <summary>
    /// Reproduce el historico de un articulo desde el ultimo cierre y dice en que se aparta
    /// de lo que hay registrado. No cambia nada: solo mira.
    /// </summary>
    public async Task<Reproduccion> Comparar(
        Guid articuloId,
        Guid almacenId,
        CancellationToken cancelacion = default)
    {
        var articulo = await articulos.PorId(articuloId, cancelacion)
            ?? throw new NoEncontrado("No existe el articulo.");

        var cierre = await cierres.Ultimo(almacenId, cancelacion);

        var apertura = Cantidad.Cero;
        var valorDeApertura = Importe.Cero;
        var fechaDeApertura = DateOnly.MinValue;

        if (cierre is not null)
        {
            fechaDeApertura = cierre.Hasta;

            var declarado = (await cierres.SaldosDe(cierre.Id, cancelacion))
                .FirstOrDefault(saldo => saldo.ArticuloId == articuloId);

            if (declarado is not null)
            {
                if (declarado.Cantidad.EnDescubierto)
                    throw new ReglaDeNegocio(
                        "El cierre dejo ese articulo en descubierto y no hay desde donde " +
                        "reproducirlo: haria falta el desglose de capas del cierre.");

                apertura = declarado.Cantidad.Disponible;
                valorDeApertura = declarado.Valor;
            }
        }

        var historico = await movimientos.Listar(
            articuloId, almacenId, cierre?.Hasta, cancelacion);

        return Recalculo.Reproducir(
            articulo.Metodo, historico, apertura, valorDeApertura, fechaDeApertura);
    }

    /// <summary>
    /// Los articulos de un almacen que conviene mirar: los que tienen algun movimiento que
    /// llego con fecha anterior a lo que ya estaba registrado.
    /// </summary>
    public Task<IReadOnlyList<Guid>> ArticulosConRetroactivos(
        Guid almacenId,
        CancellationToken cancelacion = default) =>
        movimientos.ArticulosConRetroactivos(almacenId, cancelacion);
}
