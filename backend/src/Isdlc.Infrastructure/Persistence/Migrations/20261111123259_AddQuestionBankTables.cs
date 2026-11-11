using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBankTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceDocument = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnswerMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Bands = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Questionnaires",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    QuestionSetId = table.Column<int>(type: "int", nullable: false),
                    WorkType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subcategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Participants = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssessedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questionnaires", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questionnaires_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questionnaires_QuestionSets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "QuestionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questionnaires_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questionnaires_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questionnaires_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionSetId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    WorkType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subcategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    No = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Group = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Requirement = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExpectedInfo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RiskDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuggestedAction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_QuestionSets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "QuestionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionSetId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: true),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: true),
                    Meaning = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsScored = table.Column<bool>(type: "bit", nullable: false),
                    RequiresNote = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_QuestionSets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "QuestionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionnaireId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    OptionId = table.Column<int>(type: "int", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Probability = table.Column<int>(type: "int", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionAnswers", x => x.Id);
                    table.CheckConstraint("CK_QuestionAnswers_Probability", "[Probability] IS NULL OR [Probability] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_QuestionAnswers_Severity", "[Severity] IS NULL OR [Severity] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_QuestionAnswers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionAnswers_QuestionOptions_OptionId",
                        column: x => x.OptionId,
                        principalTable: "QuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionAnswers_Questionnaires_QuestionnaireId",
                        column: x => x.QuestionnaireId,
                        principalTable: "Questionnaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionAnswers_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionAnswers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionAnswers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswers_CreatedById",
                table: "QuestionAnswers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswers_OptionId",
                table: "QuestionAnswers",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswers_ProjectId",
                table: "QuestionAnswers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswers_QuestionId",
                table: "QuestionAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswers_QuestionnaireId_QuestionId",
                table: "QuestionAnswers",
                columns: new[] { "QuestionnaireId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswers_UpdatedById",
                table: "QuestionAnswers",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Questionnaires_ApprovedById",
                table: "Questionnaires",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_Questionnaires_CreatedById",
                table: "Questionnaires",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Questionnaires_ProjectId_QuestionSetId",
                table: "Questionnaires",
                columns: new[] { "ProjectId", "QuestionSetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questionnaires_QuestionSetId",
                table: "Questionnaires",
                column: "QuestionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_Questionnaires_UpdatedById",
                table: "Questionnaires",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_Key",
                table: "QuestionOptions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionSetId",
                table: "QuestionOptions",
                column: "QuestionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Key",
                table: "Questions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_QuestionSetId_WorkType_Subcategory_Order",
                table: "Questions",
                columns: new[] { "QuestionSetId", "WorkType", "Subcategory", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionSets_Code",
                table: "QuestionSets",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionAnswers");

            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "Questionnaires");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "QuestionSets");
        }
    }
}
