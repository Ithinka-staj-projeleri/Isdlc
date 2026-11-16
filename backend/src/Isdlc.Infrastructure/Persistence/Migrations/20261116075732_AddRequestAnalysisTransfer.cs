using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestAnalysisTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "Questionnaires",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceQuestionAnswerId",
                table: "OpenIssues",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIssues_SourceQuestionAnswerId",
                table: "OpenIssues",
                column: "SourceQuestionAnswerId",
                unique: true,
                filter: "[SourceQuestionAnswerId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_OpenIssues_QuestionAnswers_SourceQuestionAnswerId",
                table: "OpenIssues",
                column: "SourceQuestionAnswerId",
                principalTable: "QuestionAnswers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpenIssues_QuestionAnswers_SourceQuestionAnswerId",
                table: "OpenIssues");

            migrationBuilder.DropIndex(
                name: "IX_OpenIssues_SourceQuestionAnswerId",
                table: "OpenIssues");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Questionnaires");

            migrationBuilder.DropColumn(
                name: "SourceQuestionAnswerId",
                table: "OpenIssues");
        }
    }
}
