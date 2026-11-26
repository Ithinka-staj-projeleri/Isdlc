using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceStepConditionsWithRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Conditions",
                table: "ProcessSteps",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "[]");

            // Every company's copy keeps its conditions: the three fixed ones become rules, in the order the old
            // check used (external request, offer, level). A level not decided yet still counts as met.
            migrationBuilder.Sql(@"
UPDATE ProcessSteps SET Conditions = '[' + ISNULL(STUFF(
      CASE WHEN RequiresExternalRequest = 1 THEN ',{""field"":""source"",""op"":""equals"",""value"":""External""}' ELSE '' END
    + CASE WHEN RequiresOffer = 1 THEN ',{""field"":""requiresOffer"",""op"":""equals"",""value"":true}' ELSE '' END
    + CASE WHEN MinLevel IS NOT NULL
        THEN ',{""field"":""level"",""op"":""atLeast"",""value"":' + CAST(MinLevel AS nvarchar(10)) + ',""whenEmpty"":""met""}'
        ELSE '' END,
    1, 1, ''), '') + ']';");

            migrationBuilder.DropColumn(
                name: "MinLevel",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "RequiresExternalRequest",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "RequiresOffer",
                table: "ProcessSteps");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinLevel",
                table: "ProcessSteps",
                type: "int",
                nullable: true);

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

            // Only the three rules this migration wrote can go back; other rules are lost.
            migrationBuilder.Sql(@"
UPDATE ProcessSteps SET
    RequiresExternalRequest = CASE WHEN Conditions LIKE '%""field"":""source"",""op"":""equals"",""value"":""External""%' THEN 1 ELSE 0 END,
    RequiresOffer = CASE WHEN Conditions LIKE '%""field"":""requiresOffer"",""op"":""equals"",""value"":true%' THEN 1 ELSE 0 END,
    MinLevel = (SELECT TRY_CAST(JSON_VALUE(r.value, '$.value') AS int) FROM OPENJSON(Conditions) r
                WHERE JSON_VALUE(r.value, '$.field') = 'level' AND JSON_VALUE(r.value, '$.op') = 'atLeast');");

            migrationBuilder.DropColumn(
                name: "Conditions",
                table: "ProcessSteps");
        }
    }
}
