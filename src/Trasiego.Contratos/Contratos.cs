using Trasiego.Dominio.Acceso;
using Trasiego.Dominio.Almacenes;
using Trasiego.Dominio.Catalogo;
using Trasiego.Dominio.Documentos;
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
    MetodoDeValoracion Metodo = MetodoDeValoracion.Fifo,
    bool LlevaLotes = false);

public record CambioDeMetodo(MetodoDeValoracion Metodo);

public record ArticuloVisto(
    Guid Id,
    string Referencia,
    string Nombre,
    UnidadDeMedida Unidad,
    MetodoDeValoracion Metodo,
    bool LlevaLotes,
    bool Activo)
{
    public static ArticuloVisto De(Articulo articulo) => new(
        articulo.Id, articulo.Referencia, articulo.Nombre,
        articulo.Unidad, articulo.Metodo, articulo.LlevaLotes, articulo.Activo);
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

// ---- Documentos ---------------------------------------------------------------------

public record AbrirDocumento(
    TipoDeDocumento Tipo,
    string Numero,
    Guid AlmacenId,
    DateOnly FechaContable,
    Guid? AlmacenDestinoId = null,
    string? Concepto = null);

public record LineaPedida(
    Guid ArticuloId,
    decimal Cantidad,
    decimal Coste = 0m,
    string? Lote = null,
    DateOnly? Caducidad = null);

public record LineaVista(
    Guid Id,
    Guid ArticuloId,
    int Orden,
    decimal Cantidad,
    decimal Coste,
    string? Lote,
    DateOnly? Caducidad);

public record DocumentoVisto(
    Guid Id,
    TipoDeDocumento Tipo,
    string Numero,
    Guid AlmacenId,
    Guid? AlmacenDestinoId,
    DateOnly FechaContable,
    string? Concepto,
    EstadoDeDocumento Estado,
    IReadOnlyList<LineaVista> Lineas)
{
    public static DocumentoVisto De(Documento documento) => new(
        documento.Id, documento.Tipo, documento.Numero,
        documento.AlmacenId, documento.AlmacenDestinoId,
        documento.FechaContable, documento.Concepto, documento.Estado,
        [.. documento.Lineas.OrderBy(linea => linea.Orden).Select(linea => new LineaVista(
            linea.Id, linea.ArticuloId, linea.Orden,
            linea.Cantidad.Valor, linea.Coste.Visible,
            linea.Lote, linea.Caducidad))]);
}

/// <summary>Lo que queda de un lote en un almacen, y hasta cuando vale.</summary>
public record LineaDeLoteVista(
    Guid ArticuloId,
    string Referencia,
    string Nombre,
    string? Lote,
    DateOnly? Caducidad,
    decimal Cantidad,
    decimal Valor,
    bool Caducado);

public record EntradaPedida(
    Guid ArticuloId,
    Guid AlmacenId,
    decimal Cantidad,
    decimal Coste,
    DateOnly FechaContable,
    string? Concepto = null,
    string? Lote = null,
    DateOnly? Caducidad = null);

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
    bool Retroactivo,
    Guid? DocumentoId,
    Guid? UsuarioId)
{
    public static MovimientoVisto De(Movimiento movimiento) => new(
        movimiento.Id, movimiento.ArticuloId, movimiento.AlmacenId,
        movimiento.Tipo, movimiento.Motivo,
        movimiento.Cantidad.Valor, movimiento.Coste.Visible,
        movimiento.FechaContable, movimiento.MomentoDeRegistro,
        movimiento.Concepto, movimiento.Retroactivo, movimiento.DocumentoId,
        movimiento.UsuarioId);
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
    bool Retroactivo,
    string? Documento,
    string? Usuario);

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
    decimal ValorAhora);

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

public record CosteDescuadrado(
    Guid MovimientoId,
    decimal Registrado,
    decimal Reproducido,
    decimal Diferencia);

public record ReproduccionVista(
    decimal Cantidad,
    decimal Valor,
    IReadOnlyList<CosteDescuadrado> Descuadrados,
    IReadOnlyList<Guid> OtrosAlmacenes);
