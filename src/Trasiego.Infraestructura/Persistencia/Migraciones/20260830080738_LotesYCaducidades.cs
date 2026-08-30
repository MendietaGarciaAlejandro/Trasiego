using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class LotesYCaducidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "Caducidad",
                table: "LineasDeDocumento",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lote",
                table: "LineasDeDocumento",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Caducidad",
                table: "CapasDeExistencias",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lote",
                table: "CapasDeExistencias",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LlevaLotes",
                table: "Articulos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CapasDeExistencias_Caducidades",
                table: "CapasDeExistencias",
                columns: new[] { "AlmacenId", "Caducidad" },
                filter: "CantidadRestante > 0 AND Caducidad IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CapasDeExistencias_Caducidades",
                table: "CapasDeExistencias");

            migrationBuilder.DropColumn(
                name: "Caducidad",
                table: "LineasDeDocumento");

            migrationBuilder.DropColumn(
                name: "Lote",
                table: "LineasDeDocumento");

            migrationBuilder.DropColumn(
                name: "Caducidad",
                table: "CapasDeExistencias");

            migrationBuilder.DropColumn(
                name: "Lote",
                table: "CapasDeExistencias");

            migrationBuilder.DropColumn(
                name: "LlevaLotes",
                table: "Articulos");
        }
    }
}
