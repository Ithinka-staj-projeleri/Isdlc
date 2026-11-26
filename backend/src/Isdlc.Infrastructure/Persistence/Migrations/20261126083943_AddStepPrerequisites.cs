using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStepPrerequisites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Prerequisites",
                table: "ProcessSteps",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                // Existing steps have no prerequisites; the seed sync then writes the template's list.
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Prerequisites",
                table: "ProcessSteps");
        }
    }
}
