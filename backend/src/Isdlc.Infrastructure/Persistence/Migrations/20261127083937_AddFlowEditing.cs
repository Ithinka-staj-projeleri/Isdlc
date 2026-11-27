using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlowEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing stage and step stays in use.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ProcessSteps",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ProcessStages",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProcessFlows",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "UsedFor",
                table: "ProcessFlows",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Each company's copies of the templates start the records the code used to find them for.
            migrationBuilder.Sql(@"
UPDATE ProcessFlows SET UsedFor = CASE TemplateCode
    WHEN 'new-software-project' THEN 'NewRequest'
    WHEN 'add-on' THEN 'AdditionalModule'
    WHEN 'process-improvement' THEN 'Improvement'
    WHEN 'resource-planning' THEN 'ResourcePlanning'
    WHEN 'billing' THEN 'Billing'
END
WHERE TemplateCode IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFlows_TenantId_UsedFor",
                table: "ProcessFlows",
                columns: new[] { "TenantId", "UsedFor" },
                unique: true,
                filter: "[UsedFor] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessFlows_TenantId_UsedFor",
                table: "ProcessFlows");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ProcessStages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProcessFlows");

            migrationBuilder.DropColumn(
                name: "UsedFor",
                table: "ProcessFlows");
        }
    }
}
