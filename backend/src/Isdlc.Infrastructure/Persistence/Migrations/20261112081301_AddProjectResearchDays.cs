using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectResearchDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResearchDays",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_ResearchDays",
                table: "Projects",
                sql: "[ResearchDays] BETWEEN 1 AND 30");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_ResearchDays",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ResearchDays",
                table: "Projects");
        }
    }
}
