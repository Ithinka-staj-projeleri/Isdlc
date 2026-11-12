using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingVisitFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Participants",
                table: "Meetings",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            // "Elif Yılmaz, Can Öztürk" → [{"name":"Elif Yılmaz"},{"name":"Can Öztürk"}], in the same order.
            // OPENJSON over the split text keeps the order on every compatibility level (STRING_SPLIT may not).
            migrationBuilder.Sql("""
                UPDATE m SET Participants = COALESCE((
                    SELECT LTRIM(RTRIM(j.value)) AS [name]
                    FROM OPENJSON(N'["' + REPLACE(STRING_ESCAPE(m.Participants, 'json'), N',', N'","') + N'"]') AS j
                    WHERE LTRIM(RTRIM(j.value)) <> N''
                    ORDER BY CAST(j.[key] AS int)
                    FOR JSON PATH), N'[]')
                FROM Meetings AS m
                WHERE ISJSON(m.Participants) = 0;
                """);

            migrationBuilder.AddColumn<string>(
                name: "CompletedActivities",
                table: "Meetings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedStages",
                table: "Meetings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextSteps",
                table: "Meetings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OngoingStages",
                table: "Meetings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlannedActivities",
                table: "Meetings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            // Existing minutes were written by whoever saved them.
            migrationBuilder.AddColumn<int>(
                name: "RecordedById",
                table: "Meetings",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE Meetings SET RecordedById = CreatedById;");

            migrationBuilder.AlterColumn<int>(
                name: "RecordedById",
                table: "Meetings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReturnDate",
                table: "Meetings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Meetings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_RecordedById",
                table: "Meetings",
                column: "RecordedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Users_RecordedById",
                table: "Meetings",
                column: "RecordedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Users_RecordedById",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_RecordedById",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "CompletedActivities",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "CompletedStages",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "NextSteps",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "OngoingStages",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "PlannedActivities",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "RecordedById",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "ReturnDate",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Meetings");

            // Back to "Elif Yılmaz, Can Öztürk" (roles are dropped).
            migrationBuilder.Sql("""
                UPDATE m SET Participants = LEFT(COALESCE((
                    SELECT STRING_AGG(JSON_VALUE(j.value, '$.name'), N', ') WITHIN GROUP (ORDER BY CAST(j.[key] AS int))
                    FROM OPENJSON(m.Participants) AS j), N''), 1000)
                FROM Meetings AS m
                WHERE ISJSON(m.Participants) = 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Participants",
                table: "Meetings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 10000);
        }
    }
}
