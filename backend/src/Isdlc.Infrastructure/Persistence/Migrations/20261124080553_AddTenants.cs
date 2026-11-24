using System;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Tenants;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuestionSets_Code",
                table: "QuestionSets");

            migrationBuilder.DropIndex(
                name: "IX_Questions_Key",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionOptions_Key",
                table: "QuestionOptions");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Code",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProcessFlows_Code",
                table: "ProcessFlows");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StepRecords",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Risks",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Requirements",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "RequestDecisions",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomized",
                table: "QuestionSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                table: "QuestionSets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "QuestionSets",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Questions",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "QuestionOptions",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Questionnaires",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "QuestionAnswers",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProjectDocuments",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProcessSteps",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProcessStepJumps",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProcessStages",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProcessImprovements",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomized",
                table: "ProcessFlows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                table: "ProcessFlows",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProcessFlows",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "OpenIssues",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Meetings",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomized",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                table: "DocumentTypes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "DocumentTypes",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: Tenant.IthinkaId);

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AiEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AiDataSharingAllowed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.Sql(TenantMigrationSql.InsertIthinka);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StepRecords_TenantId",
                table: "StepRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_TenantId",
                table: "Risks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_TenantId",
                table: "Requirements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDecisions_TenantId",
                table: "RequestDecisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionSets_TenantId_Code",
                table: "QuestionSets",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TenantId_Key",
                table: "Questions",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_TenantId_Key",
                table: "QuestionOptions",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questionnaires_TenantId",
                table: "Questionnaires",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswers_TenantId",
                table: "QuestionAnswers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_Code",
                table: "Projects",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocuments_TenantId",
                table: "ProjectDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_TenantId",
                table: "ProcessSteps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepJumps_TenantId",
                table: "ProcessStepJumps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStages_TenantId",
                table: "ProcessStages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessImprovements_TenantId",
                table: "ProcessImprovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFlows_TenantId_Code",
                table: "ProcessFlows",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIssues_TenantId",
                table: "OpenIssues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_TenantId",
                table: "Meetings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_TenantId_Code",
                table: "DocumentTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTypes_Tenants_TenantId",
                table: "DocumentTypes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Tenants_TenantId",
                table: "Meetings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Tenants_TenantId",
                table: "Notifications",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OpenIssues_Tenants_TenantId",
                table: "OpenIssues",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessFlows_Tenants_TenantId",
                table: "ProcessFlows",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessImprovements_Tenants_TenantId",
                table: "ProcessImprovements",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessStages_Tenants_TenantId",
                table: "ProcessStages",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessStepJumps_Tenants_TenantId",
                table: "ProcessStepJumps",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessSteps_Tenants_TenantId",
                table: "ProcessSteps",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDocuments_Tenants_TenantId",
                table: "ProjectDocuments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionAnswers_Tenants_TenantId",
                table: "QuestionAnswers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Questionnaires_Tenants_TenantId",
                table: "Questionnaires",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_Tenants_TenantId",
                table: "QuestionOptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Tenants_TenantId",
                table: "Questions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionSets_Tenants_TenantId",
                table: "QuestionSets",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestDecisions_Tenants_TenantId",
                table: "RequestDecisions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Requirements_Tenants_TenantId",
                table: "Requirements",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Risks_Tenants_TenantId",
                table: "Risks",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StepRecords_Tenants_TenantId",
                table: "StepRecords",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(TenantMigrationSql.AfterColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentTypes_Tenants_TenantId",
                table: "DocumentTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Tenants_TenantId",
                table: "Meetings");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Tenants_TenantId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_OpenIssues_Tenants_TenantId",
                table: "OpenIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessFlows_Tenants_TenantId",
                table: "ProcessFlows");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessImprovements_Tenants_TenantId",
                table: "ProcessImprovements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessStages_Tenants_TenantId",
                table: "ProcessStages");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessStepJumps_Tenants_TenantId",
                table: "ProcessStepJumps");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessSteps_Tenants_TenantId",
                table: "ProcessSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDocuments_Tenants_TenantId",
                table: "ProjectDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionAnswers_Tenants_TenantId",
                table: "QuestionAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_Questionnaires_Tenants_TenantId",
                table: "Questionnaires");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionOptions_Tenants_TenantId",
                table: "QuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Tenants_TenantId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionSets_Tenants_TenantId",
                table: "QuestionSets");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestDecisions_Tenants_TenantId",
                table: "RequestDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Requirements_Tenants_TenantId",
                table: "Requirements");

            migrationBuilder.DropForeignKey(
                name: "FK_Risks_Tenants_TenantId",
                table: "Risks");

            migrationBuilder.DropForeignKey(
                name: "FK_StepRecords_Tenants_TenantId",
                table: "StepRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_StepRecords_TenantId",
                table: "StepRecords");

            migrationBuilder.DropIndex(
                name: "IX_Risks_TenantId",
                table: "Risks");

            migrationBuilder.DropIndex(
                name: "IX_Requirements_TenantId",
                table: "Requirements");

            migrationBuilder.DropIndex(
                name: "IX_RequestDecisions_TenantId",
                table: "RequestDecisions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionSets_TenantId_Code",
                table: "QuestionSets");

            migrationBuilder.DropIndex(
                name: "IX_Questions_TenantId_Key",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionOptions_TenantId_Key",
                table: "QuestionOptions");

            migrationBuilder.DropIndex(
                name: "IX_Questionnaires_TenantId",
                table: "Questionnaires");

            migrationBuilder.DropIndex(
                name: "IX_QuestionAnswers_TenantId",
                table: "QuestionAnswers");

            migrationBuilder.DropIndex(
                name: "IX_Projects_TenantId_Code",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDocuments_TenantId",
                table: "ProjectDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_TenantId",
                table: "ProcessSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessStepJumps_TenantId",
                table: "ProcessStepJumps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessStages_TenantId",
                table: "ProcessStages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessImprovements_TenantId",
                table: "ProcessImprovements");

            migrationBuilder.DropIndex(
                name: "IX_ProcessFlows_TenantId_Code",
                table: "ProcessFlows");

            migrationBuilder.DropIndex(
                name: "IX_OpenIssues_TenantId",
                table: "OpenIssues");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_TenantId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTypes_TenantId_Code",
                table: "DocumentTypes");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StepRecords");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Risks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RequestDecisions");

            migrationBuilder.DropColumn(
                name: "IsCustomized",
                table: "QuestionSets");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                table: "QuestionSets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "QuestionSets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "QuestionOptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Questionnaires");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "QuestionAnswers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProjectDocuments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProcessStepJumps");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProcessStages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProcessImprovements");

            migrationBuilder.DropColumn(
                name: "IsCustomized",
                table: "ProcessFlows");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                table: "ProcessFlows");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProcessFlows");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OpenIssues");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "IsCustomized",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionSets_Code",
                table: "QuestionSets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Key",
                table: "Questions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_Key",
                table: "QuestionOptions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                table: "Projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFlows_Code",
                table: "ProcessFlows",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);
        }
    }
}
