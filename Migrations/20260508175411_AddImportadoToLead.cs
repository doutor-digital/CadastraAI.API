using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadastraAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddImportadoToLead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Importado",
                table: "leads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_leads_EmpresaId_Importado",
                table: "leads",
                columns: new[] { "EmpresaId", "Importado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leads_EmpresaId_Importado",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "Importado",
                table: "leads");
        }
    }
}
