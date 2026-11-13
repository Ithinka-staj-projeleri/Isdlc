using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAddOnParentProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentProjectId",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ParentProjectId",
                table: "Projects",
                column: "ParentProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Projects_ParentProjectId",
                table: "Projects",
                column: "ParentProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Projects_ParentProjectId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ParentProjectId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ParentProjectId",
                table: "Projects");
        }
    }
}
