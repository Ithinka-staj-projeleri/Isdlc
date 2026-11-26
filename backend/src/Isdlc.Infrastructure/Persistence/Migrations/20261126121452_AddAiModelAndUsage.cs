using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiModelAndUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiModel",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiMonthlyTokenLimit",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiUsages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    Assistant = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    AudioSeconds = table.Column<int>(type: "int", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsages_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiUsages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tenants_AiMonthlyTokenLimit",
                table: "Tenants",
                sql: "[AiMonthlyTokenLimit] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsages_ProjectId",
                table: "AiUsages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsages_TenantId_CreatedAt",
                table: "AiUsages",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsages_UserId",
                table: "AiUsages",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tenants_AiMonthlyTokenLimit",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AiModel",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AiMonthlyTokenLimit",
                table: "Tenants");
        }
    }
}
