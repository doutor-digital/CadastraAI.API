using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadastraAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantScopingCompareceuMotivos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tenant scoping é introduzido aqui — leads/consultas/tratamentos/recebimentos
            // pré-existentes não têm empresa associada e ficariam órfãos. Como o sistema acabou
            // de subir e os dados são de teste, limpa antes de adicionar a coluna NOT NULL.
            migrationBuilder.Sql("DELETE FROM \"leads\";");

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "leads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "Compareceu",
                table: "consultas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "motivos_nao_fechamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_motivos_nao_fechamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_motivos_nao_fechamento_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leads_EmpresaId_CreatedAt",
                table: "leads",
                columns: new[] { "EmpresaId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_motivos_nao_fechamento_EmpresaId_Nome",
                table: "motivos_nao_fechamento",
                columns: new[] { "EmpresaId", "Nome" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_leads_empresas_EmpresaId",
                table: "leads",
                column: "EmpresaId",
                principalTable: "empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Seed dos 9 motivos default + cor do semáforo em todas as empresas existentes.
            migrationBuilder.Sql(@"
                INSERT INTO motivos_nao_fechamento (""Id"", ""EmpresaId"", ""Nome"", ""Cor"", ""IsDefault"", ""CreatedAt"")
                SELECT gen_random_uuid(), e.""Id"", m.nome, m.cor, true, NOW() AT TIME ZONE 'UTC'
                FROM empresas e
                CROSS JOIN (VALUES
                    ('Fechou tratamento total', 'verde'),
                    ('Fechou tratamento parcial', 'verde'),
                    ('Assinou contrato, sem entrada', 'amarelo'),
                    ('Vai decidir com familiares', 'amarelo'),
                    ('Vai verificar a melhor forma de pagamento', 'amarelo'),
                    ('Solicitado exame de imagem', 'amarelo'),
                    ('Mora fora +50km', 'vermelho'),
                    ('Outra patologia', 'vermelho'),
                    ('Sem condições financeiras', 'vermelho')
                ) AS m(nome, cor)
                ON CONFLICT (""EmpresaId"", ""Nome"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_leads_empresas_EmpresaId",
                table: "leads");

            migrationBuilder.DropTable(
                name: "motivos_nao_fechamento");

            migrationBuilder.DropIndex(
                name: "IX_leads_EmpresaId_CreatedAt",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "Compareceu",
                table: "consultas");
        }
    }
}
