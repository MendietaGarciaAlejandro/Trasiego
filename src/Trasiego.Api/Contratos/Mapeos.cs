using Trasiego.Aplicacion.Cierres;
using Trasiego.Contratos;
using Trasiego.Dominio.Valoracion;

namespace Trasiego.Api.Contratos;

/// <summary>
/// Los contratos viven en su propio proyecto para que los pueda usar tambien el cliente de
/// escritorio, y por eso no conocen los tipos de la capa de aplicacion. La traduccion se
/// queda aqui.
/// </summary>
public static class Mapeos
{
    public static DescuadreVisto Visto(this Descuadre descuadre) => new(
        descuadre.ArticuloId,
        descuadre.CantidadDeclarada.Valor, descuadre.CantidadAhora.Valor,
        descuadre.ValorDeclarado.Visible, descuadre.ValorAhora.Visible);

    public static ReproduccionVista Vista(this Reproduccion reproduccion) => new(
        reproduccion.Cantidad.Valor,
        reproduccion.Valor.Visible,
        [.. reproduccion.Descuadradas.Select(salida => new SalidaDescuadrada(
            salida.MovimientoId,
            salida.Registrado.Visible,
            salida.Reproducido.Visible,
            salida.Diferencia.Visible))]);
}
