using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadastraAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dashboard_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dashboard_snapshots_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dashboard_snapshots_users_CapturedByUserId",
                        column: x => x.CapturedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_snapshots_CapturedByUserId",
                table: "dashboard_snapshots",
                column: "CapturedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_snapshots_EmpresaId_CapturedAt",
                table: "dashboard_snapshots",
                columns: new[] { "EmpresaId", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_snapshots");
        }
    }
}
