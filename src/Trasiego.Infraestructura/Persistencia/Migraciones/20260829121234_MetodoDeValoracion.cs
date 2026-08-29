using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trasiego.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MetodoDeValoracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Metodo",
                table: "Articulos",
                type: "int",
                nullable: false,
                // Fifo, que es el valor 1 del enum. EF pone 0 por defecto y ahi no hay
                // ningun metodo: los articulos que ya existieran quedarian sin criterio.
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Metodo",
                table: "Articulos");
        }
    }
}
