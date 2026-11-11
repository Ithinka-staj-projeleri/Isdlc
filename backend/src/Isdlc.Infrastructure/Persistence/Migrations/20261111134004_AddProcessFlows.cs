using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_Code",
                table: "ProcessSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_Order",
                table: "ProcessSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessStages_Order",
                table: "ProcessStages");

            migrationBuilder.AddColumn<int>(
                name: "ProcessFlowId",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresOffer",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinLevel",
                table: "ProcessSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessFlowId",
                table: "ProcessSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresExternalRequest",
                table: "ProcessSteps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresOffer",
                table: "ProcessSteps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProcessFlowId",
                table: "ProcessStages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProcessFlows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProjectStageOrder = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessFlows", x => x.Id);
                });

            // An existing database already has the 13-stage flow and projects on it: they become the
            // "isdlc-v12" flow (Seed/flows/isdlc-v12.json), so the foreign keys below find a flow.
            // A new database has no rows here; the seeder adds the flow from its file.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ProcessStages)
                BEGIN
                    INSERT INTO ProcessFlows (Code, Name, Kind, ProjectStageOrder)
                    VALUES (N'isdlc-v12', N'ISDLC Yeni Yazılım Projesi (v12)', N'Request', 4);

                    DECLARE @flowId int = SCOPE_IDENTITY();
                    UPDATE ProcessStages SET ProcessFlowId = @flowId;
                    UPDATE ProcessSteps SET ProcessFlowId = @flowId;
                    -- Until the request form asks "teklif hazırlanacak mı" (Faz 4, D1), every request may get an offer.
                    UPDATE Projects SET ProcessFlowId = @flowId, RequiresOffer = 1;
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProcessFlowId",
                table: "Projects",
                column: "ProcessFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ProcessFlowId_Code",
                table: "ProcessSteps",
                columns: new[] { "ProcessFlowId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ProcessFlowId_Order",
                table: "ProcessSteps",
                columns: new[] { "ProcessFlowId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStages_ProcessFlowId_Order",
                table: "ProcessStages",
                columns: new[] { "ProcessFlowId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFlows_Code",
                table: "ProcessFlows",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessStages_ProcessFlows_ProcessFlowId",
                table: "ProcessStages",
                column: "ProcessFlowId",
                principalTable: "ProcessFlows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessSteps_ProcessFlows_ProcessFlowId",
                table: "ProcessSteps",
                column: "ProcessFlowId",
                principalTable: "ProcessFlows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProcessFlows_ProcessFlowId",
                table: "Projects",
                column: "ProcessFlowId",
                principalTable: "ProcessFlows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessStages_ProcessFlows_ProcessFlowId",
                table: "ProcessStages");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessSteps_ProcessFlows_ProcessFlowId",
                table: "ProcessSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProcessFlows_ProcessFlowId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProcessFlows");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ProcessFlowId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_ProcessFlowId_Code",
                table: "ProcessSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_ProcessFlowId_Order",
                table: "ProcessSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessStages_ProcessFlowId_Order",
                table: "ProcessStages");

            migrationBuilder.DropColumn(
                name: "ProcessFlowId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RequiresOffer",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "MinLevel",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "ProcessFlowId",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "RequiresExternalRequest",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "RequiresOffer",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "ProcessFlowId",
                table: "ProcessStages");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_Code",
                table: "ProcessSteps",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_Order",
                table: "ProcessSteps",
                column: "Order",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStages_Order",
                table: "ProcessStages",
                column: "Order",
                unique: true);
        }
    }
}
