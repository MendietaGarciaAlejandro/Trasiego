using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class StockNegativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PermiteDescubierto",
                table: "Almacenes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Descubiertos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticuloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovimientoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Coste = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CantidadCubierta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CosteCubierto = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Descubiertos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Descubiertos_ArticuloId_AlmacenId",
                table: "Descubiertos",
                columns: new[] { "ArticuloId", "AlmacenId" });

            migrationBuilder.CreateIndex(
                name: "IX_Descubiertos_MovimientoId",
                table: "Descubiertos",
                column: "MovimientoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Descubiertos");

            migrationBuilder.DropColumn(
                name: "PermiteDescubierto",
                table: "Almacenes");
        }
    }
}
