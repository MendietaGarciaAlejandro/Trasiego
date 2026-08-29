using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CierreDePeriodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Retroactivo",
                table: "Movimientos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Cierres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    MomentoDeCierre = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cierres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaldosDeCierre",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CierreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticuloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaldosDeCierre", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_Retroactivos",
                table: "Movimientos",
                columns: new[] { "ArticuloId", "AlmacenId" },
                filter: "Retroactivo = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Cierres_AlmacenId_Hasta",
                table: "Cierres",
                columns: new[] { "AlmacenId", "Hasta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaldosDeCierre_CierreId_ArticuloId",
                table: "SaldosDeCierre",
                columns: new[] { "CierreId", "ArticuloId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cierres");

            migrationBuilder.DropTable(
                name: "SaldosDeCierre");

            migrationBuilder.DropIndex(
                name: "IX_Movimientos_Retroactivos",
                table: "Movimientos");

            migrationBuilder.DropColumn(
                name: "Retroactivo",
                table: "Movimientos");
        }
    }
}
