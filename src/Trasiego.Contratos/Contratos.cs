using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Cierres;
using Trasiego.Dominio.Movimientos;
using Trasiego.Dominio.Valoracion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Contratos;

// Lo que entra y lo que sale de la Api va en decimal y no en Cantidad ni Importe. Esos tipos
// existen para que dentro no se pueda operar mal con ellos; fuera solo estorbarian, porque
// serializados serian un objeto con un campo dentro.

public record EstadoDeSalud(string Estado);

// ---- Acceso -------------------------------------------------------------------------

public record AccesoPedido(string Correo, string Contrasena);

public record EntradaVista(string Token, string Nombre, RolDeUsuario Rol);

public record AltaDeUsuario(string Correo, string Nombre, string Contrasena, RolDeUsuario Rol);

public record UsuarioVisto(Guid Id, string Correo, string Nombre, RolDeUsuario Rol, bool Activo)
{
    public static UsuarioVisto De(Usuario usuario) => new(
        usuario.Id, usuario.Correo, usuario.Nombre, usuario.Rol, usuario.Activo);
}

// ---- Catalogo -----------------------------------------------------------------------

public record AltaDeArticulo(
    string Referencia,
    string Nombre,
    UnidadDeMedida Unidad,
    MetodoDeValoracion Metodo = MetodoDeValoracion.Fifo);

public record CambioDeMetodo(MetodoDeValoracion Metodo);

public record ArticuloVisto(
    Guid Id,
    string Referencia,
    string Nombre,
    UnidadDeMedida Unidad,
    MetodoDeValoracion Metodo,
    bool Activo)
{
    public static ArticuloVisto De(Articulo articulo) => new(
        articulo.Id, articulo.Referencia, articulo.Nombre,
        articulo.Unidad, articulo.Metodo, articulo.Activo);
}

public record AltaDeAlmacen(string Codigo, string Nombre, bool PermiteDescubierto = false);

public record AlmacenVisto(
    Guid Id,
    string Codigo,
    string Nombre,
    bool PermiteDescubierto,
    bool Activo)
{
    public static AlmacenVisto De(Almacen almacen) => new(
        almacen.Id, almacen.Codigo, almacen.Nombre,
        almacen.PermiteDescubierto, almacen.Activo);
}

// ---- Movimientos --------------------------------------------------------------------

public record EntradaPedida(
    Guid ArticuloId,
    Guid AlmacenId,
    decimal Cantidad,
    decimal Coste,
    DateOnly FechaContable,
    string? Concepto = null);

public record SalidaPedida(
    Guid ArticuloId,
    Guid AlmacenId,
    decimal Cantidad,
    DateOnly FechaContable,
    string? Concepto = null);

public record DevolucionPedida(
    Guid SalidaId,
    decimal Cantidad,
    DateOnly FechaContable,
    string? Concepto = null);

public record TraspasoPedido(
    Guid ArticuloId,
    Guid OrigenId,
    Guid DestinoId,
    decimal Cantidad,
    DateOnly FechaContable,
    string? Concepto = null);

public record TraspasoVisto(MovimientoVisto Salida, MovimientoVisto Entrada);

public record RecuentoPedido(
    Guid ArticuloId,
    Guid AlmacenId,
    decimal Contada,
    DateOnly FechaContable,
    string? Concepto = null);

public record MovimientoVisto(
    Guid Id,
    Guid ArticuloId,
    Guid AlmacenId,
    TipoDeMovimiento Tipo,
    MotivoDeMovimiento Motivo,
    decimal Cantidad,
    decimal Coste,
    DateOnly FechaContable,
    DateTimeOffset MomentoDeRegistro,
    string? Concepto,
    bool Retroactivo)
{
    public static MovimientoVisto De(Movimiento movimiento) => new(
        movimiento.Id, movimiento.ArticuloId, movimiento.AlmacenId,
        movimiento.Tipo, movimiento.Motivo,
        movimiento.Cantidad.Valor, movimiento.Coste.Visible,
        movimiento.FechaContable, movimiento.MomentoDeRegistro,
        movimiento.Concepto, movimiento.Retroactivo);
}

public record ExistenciasVistas(Guid ArticuloId, Guid AlmacenId, decimal Saldo, decimal Valor);

/// <summary>
/// Una linea del kardex: el movimiento y como quedaba el almacen despues de el, en cantidad
/// y en dinero.
/// </summary>
public record LineaDeKardex(
    Guid MovimientoId,
    DateOnly FechaContable,
    TipoDeMovimiento Tipo,
    MotivoDeMovimiento Motivo,
    string? Concepto,
    decimal Cantidad,
    decimal Coste,
    decimal SaldoCantidad,
    decimal SaldoValor,
    bool Retroactivo);

// ---- Cierres y recalculo ------------------------------------------------------------

public record CierrePedido(Guid AlmacenId, DateOnly Hasta, string? Concepto = null);

public record CierreVisto(
    Guid Id,
    Guid AlmacenId,
    DateOnly Hasta,
    DateTimeOffset MomentoDeCierre,
    string? Concepto)
{
    public static CierreVisto De(Cierre cierre) => new(
        cierre.Id, cierre.AlmacenId, cierre.Hasta, cierre.MomentoDeCierre, cierre.Concepto);
}

public record DescuadreVisto(
    Guid ArticuloId,
    decimal CantidadDeclarada,
    decimal CantidadAhora,
    decimal ValorDeclarado,
    decimal ValorAhora)
{
}

public record LineaDeValoracionVista(
    Guid ArticuloId,
    string Referencia,
    string Nombre,
    decimal Cantidad,
    decimal Valor);

public record ValoracionVista(
    DateOnly Fecha,
    decimal Total,
    IReadOnlyList<LineaDeValoracionVista> Lineas);

public record SalidaDescuadrada(
    Guid MovimientoId,
    decimal Registrado,
    decimal Reproducido,
    decimal Diferencia);

public record ReproduccionVista(
    decimal Cantidad,
    decimal Valor,
    IReadOnlyList<SalidaDescuadrada> Descuadradas)
{
}
