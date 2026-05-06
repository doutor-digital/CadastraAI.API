using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadastraAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaLogoUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "empresas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "empresas");
        }
    }
}
