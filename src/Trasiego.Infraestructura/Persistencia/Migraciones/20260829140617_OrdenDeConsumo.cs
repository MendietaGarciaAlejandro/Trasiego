using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class OrdenDeConsumo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConsumosDeCapa_MovimientoId",
                table: "ConsumosDeCapa");

            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "ConsumosDeCapa",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosDeCapa_MovimientoId_Orden",
                table: "ConsumosDeCapa",
                columns: new[] { "MovimientoId", "Orden" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConsumosDeCapa_MovimientoId_Orden",
                table: "ConsumosDeCapa");

            migrationBuilder.DropColumn(
                name: "Orden",
                table: "ConsumosDeCapa");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosDeCapa_MovimientoId",
                table: "ConsumosDeCapa",
                column: "MovimientoId");
        }
    }
}
