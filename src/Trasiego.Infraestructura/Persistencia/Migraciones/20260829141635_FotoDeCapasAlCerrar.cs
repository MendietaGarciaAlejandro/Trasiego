using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FotoDeCapasAlCerrar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FotosDeCapa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CierreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticuloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Coste = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    FechaContable = table.Column<DateOnly>(type: "date", nullable: false),
                    MomentoDeRegistro = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosDeCapa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosDeCapa_CierreId_ArticuloId",
                table: "FotosDeCapa",
                columns: new[] { "CierreId", "ArticuloId" });

            migrationBuilder.CreateIndex(
                name: "IX_FotosDeCapa_CierreId_CapaId",
                table: "FotosDeCapa",
                columns: new[] { "CierreId", "CapaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosDeCapa");
        }
    }
}
