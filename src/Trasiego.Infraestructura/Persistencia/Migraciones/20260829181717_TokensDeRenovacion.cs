using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class TokensDeRenovacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Renovaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Huella = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Caduca = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Usado = table.Column<bool>(type: "bit", nullable: false),
                    Revocado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Renovaciones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Renovaciones_Huella",
                table: "Renovaciones",
                column: "Huella",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Renovaciones_UsuarioId",
                table: "Renovaciones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Renovaciones");
        }
    }
}
