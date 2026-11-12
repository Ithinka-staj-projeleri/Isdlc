using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessImprovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AffectedProcess = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProposedSolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuggestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: true),
                    Frequency = table.Column<int>(type: "int", nullable: true),
                    Ease = table.Column<int>(type: "int", nullable: true),
                    CurrentState = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Solution = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PilotPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PilotTarget = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CostBenefit = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PilotActual = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PilotFeedback = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PilotResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PilotDecision = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessImprovements", x => x.Id);
                    table.CheckConstraint("CK_ProcessImprovements_Ease", "[Ease] IS NULL OR [Ease] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProcessImprovements_Frequency", "[Frequency] IS NULL OR [Frequency] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProcessImprovements_Impact", "[Impact] IS NULL OR [Impact] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProcessImprovements_ScoresTogether", "([Impact] IS NULL AND [Frequency] IS NULL AND [Ease] IS NULL) OR ([Impact] IS NOT NULL AND [Frequency] IS NOT NULL AND [Ease] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProcessImprovements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessImprovements_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessImprovements_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessImprovements_CreatedById",
                table: "ProcessImprovements",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessImprovements_ProjectId",
                table: "ProcessImprovements",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessImprovements_UpdatedById",
                table: "ProcessImprovements",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessImprovements");
        }
    }
}
