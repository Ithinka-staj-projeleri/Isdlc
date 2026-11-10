using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAssessmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Effort = table.Column<int>(type: "int", nullable: true),
                    ScreensAndModules = table.Column<int>(type: "int", nullable: true),
                    Integrations = table.Column<int>(type: "int", nullable: true),
                    UserRoles = table.Column<int>(type: "int", nullable: true),
                    DataSensitivity = table.Column<int>(type: "int", nullable: true),
                    HardwareDependency = table.Column<int>(type: "int", nullable: true),
                    StrategicFit = table.Column<int>(type: "int", nullable: true),
                    CommercialValue = table.Column<int>(type: "int", nullable: true),
                    TechnicalFeasibility = table.Column<int>(type: "int", nullable: true),
                    ResourceAndSchedule = table.Column<int>(type: "int", nullable: true),
                    CustomerFit = table.Column<int>(type: "int", nullable: true),
                    RiskLevel = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAssessments", x => x.Id);
                    table.CheckConstraint("CK_ProjectAssessments_CommercialValue", "[CommercialValue] IS NULL OR [CommercialValue] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProjectAssessments_CustomerFit", "[CustomerFit] IS NULL OR [CustomerFit] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProjectAssessments_DataSensitivity", "[DataSensitivity] IS NULL OR [DataSensitivity] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_ProjectAssessments_Effort", "[Effort] IS NULL OR [Effort] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_ProjectAssessments_HardwareDependency", "[HardwareDependency] IS NULL OR [HardwareDependency] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_ProjectAssessments_Integrations", "[Integrations] IS NULL OR [Integrations] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_ProjectAssessments_ResourceAndSchedule", "[ResourceAndSchedule] IS NULL OR [ResourceAndSchedule] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProjectAssessments_RiskLevel", "[RiskLevel] IS NULL OR [RiskLevel] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProjectAssessments_ScreensAndModules", "[ScreensAndModules] IS NULL OR [ScreensAndModules] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_ProjectAssessments_StrategicFit", "[StrategicFit] IS NULL OR [StrategicFit] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProjectAssessments_TechnicalFeasibility", "[TechnicalFeasibility] IS NULL OR [TechnicalFeasibility] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ProjectAssessments_UserRoles", "[UserRoles] IS NULL OR [UserRoles] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectAssessments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAssessments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAssessments_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssessments_CreatedById",
                table: "ProjectAssessments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssessments_ProjectId",
                table: "ProjectAssessments",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssessments_UpdatedById",
                table: "ProjectAssessments",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAssessments");
        }
    }
}
