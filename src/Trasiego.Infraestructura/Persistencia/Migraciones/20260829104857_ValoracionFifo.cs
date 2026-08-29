using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ValoracionFifo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Coste",
                table: "Movimientos",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CapasDeExistencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticuloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovimientoDeEntradaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaContable = table.Column<DateOnly>(type: "date", nullable: false),
                    MomentoDeRegistro = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CantidadInicial = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CosteInicial = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CantidadRestante = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CosteRestante = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapasDeExistencias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsumosDeCapa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovimientoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Coste = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumosDeCapa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapasDeExistencias_ArticuloId_AlmacenId_FechaContable_MomentoDeRegistro",
                table: "CapasDeExistencias",
                columns: new[] { "ArticuloId", "AlmacenId", "FechaContable", "MomentoDeRegistro" },
                filter: "CantidadRestante > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosDeCapa_CapaId",
                table: "ConsumosDeCapa",
                column: "CapaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosDeCapa_MovimientoId",
                table: "ConsumosDeCapa",
                column: "MovimientoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapasDeExistencias");

            migrationBuilder.DropTable(
                name: "ConsumosDeCapa");

            migrationBuilder.DropColumn(
                name: "Coste",
                table: "Movimientos");
        }
    }
}
