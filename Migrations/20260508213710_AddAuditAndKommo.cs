using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadastraAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndKommo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "tratamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "recebimentos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "recebimentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "consultas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Meta = table.Column<string>(type: "jsonb", nullable: true),
                    Ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "kommo_inbox_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    KommoLeadId = table.Column<long>(type: "bigint", nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ImportedLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kommo_inbox_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kommo_inbox_items_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kommo_inbox_items_leads_ImportedLeadId",
                        column: x => x.ImportedLeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_kommo_inbox_items_users_ImportedByUserId",
                        column: x => x.ImportedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "kommo_integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subdomain = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    TokenSuffix = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    WebhookSecret = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kommo_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kommo_integrations_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kommo_integrations_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tratamentos_CreatedByUserId",
                table: "tratamentos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_recebimentos_CreatedByUserId",
                table: "recebimentos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_CreatedByUserId",
                table: "leads",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_consultas_CreatedByUserId",
                table: "consultas",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EmpresaId_Action",
                table: "audit_logs",
                columns: new[] { "EmpresaId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EmpresaId_At",
                table: "audit_logs",
                columns: new[] { "EmpresaId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EmpresaId_UserId",
                table: "audit_logs",
                columns: new[] { "EmpresaId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_kommo_inbox_items_EmpresaId_KommoLeadId",
                table: "kommo_inbox_items",
                columns: new[] { "EmpresaId", "KommoLeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_kommo_inbox_items_EmpresaId_Status_ReceivedAt",
                table: "kommo_inbox_items",
                columns: new[] { "EmpresaId", "Status", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_kommo_inbox_items_ImportedByUserId",
                table: "kommo_inbox_items",
                column: "ImportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_kommo_inbox_items_ImportedLeadId",
                table: "kommo_inbox_items",
                column: "ImportedLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_kommo_integrations_CreatedByUserId",
                table: "kommo_integrations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_kommo_integrations_EmpresaId",
                table: "kommo_integrations",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_consultas_users_CreatedByUserId",
                table: "consultas",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_leads_users_CreatedByUserId",
                table: "leads",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_recebimentos_users_CreatedByUserId",
                table: "recebimentos",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tratamentos_users_CreatedByUserId",
                table: "tratamentos",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_consultas_users_CreatedByUserId",
                table: "consultas");

            migrationBuilder.DropForeignKey(
                name: "FK_leads_users_CreatedByUserId",
                table: "leads");

            migrationBuilder.DropForeignKey(
                name: "FK_recebimentos_users_CreatedByUserId",
                table: "recebimentos");

            migrationBuilder.DropForeignKey(
                name: "FK_tratamentos_users_CreatedByUserId",
                table: "tratamentos");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "kommo_inbox_items");

            migrationBuilder.DropTable(
                name: "kommo_integrations");

            migrationBuilder.DropIndex(
                name: "IX_tratamentos_CreatedByUserId",
                table: "tratamentos");

            migrationBuilder.DropIndex(
                name: "IX_recebimentos_CreatedByUserId",
                table: "recebimentos");

            migrationBuilder.DropIndex(
                name: "IX_leads_CreatedByUserId",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_consultas_CreatedByUserId",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "tratamentos");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "recebimentos");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "recebimentos");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "consultas");
        }
    }
}
