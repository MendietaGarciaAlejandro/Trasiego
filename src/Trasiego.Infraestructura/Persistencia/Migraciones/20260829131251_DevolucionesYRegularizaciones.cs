using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DevolucionesYRegularizaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Motivo",
                table: "Movimientos",
                type: "int",
                nullable: false,
                // Ordinario, que es el 1 del enum: con el 0 que pone EF los movimientos que ya
                // existieran quedarian con un motivo que no existe.
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "MovimientoOrigenId",
                table: "Movimientos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CantidadDevuelta",
                table: "ConsumosDeCapa",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CosteDevuelto",
                table: "ConsumosDeCapa",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "Movimientos");

            migrationBuilder.DropColumn(
                name: "MovimientoOrigenId",
                table: "Movimientos");

            migrationBuilder.DropColumn(
                name: "CantidadDevuelta",
                table: "ConsumosDeCapa");

            migrationBuilder.DropColumn(
                name: "CosteDevuelto",
                table: "ConsumosDeCapa");
        }
    }
}
