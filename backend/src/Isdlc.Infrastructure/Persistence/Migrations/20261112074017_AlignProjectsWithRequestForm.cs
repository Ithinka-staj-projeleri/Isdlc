using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignProjectsWithRequestForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "Projects",
                newName: "RequestDate");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Projects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterContact",
                table: "Projects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Existing records (docs/projects-api.md, decision D1-4): "ISDLC-2026-014" becomes Proje No "14",
            // and the old channels map to the company form's e-mail / phone.
            migrationBuilder.Sql("""
                UPDATE Projects
                SET Code = CAST(CAST(SUBSTRING(Code, 12, 20) AS int) AS nvarchar(20))
                WHERE Code LIKE 'ISDLC-[0-9][0-9][0-9][0-9]-%'
                  AND SUBSTRING(Code, 12, 20) <> ''
                  AND SUBSTRING(Code, 12, 20) NOT LIKE '%[^0-9]%';

                UPDATE Projects SET RequestChannel = 'Email' WHERE RequestChannel = 'Written';
                UPDATE Projects SET RequestChannel = 'Phone' WHERE RequestChannel = 'Verbal';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The year of the old code is not kept; the year of the request is used instead.
            migrationBuilder.Sql("""
                UPDATE Projects SET RequestChannel = 'Written' WHERE RequestChannel = 'Email';
                UPDATE Projects SET RequestChannel = 'Verbal' WHERE RequestChannel = 'Phone';

                UPDATE Projects
                SET Code = CONCAT('ISDLC-', YEAR(RequestDate), '-', RIGHT(CONCAT('00', Code), CASE WHEN LEN(Code) > 3 THEN LEN(Code) ELSE 3 END))
                WHERE Code <> '' AND Code NOT LIKE '%[^0-9]%';
                """);

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RequesterContact",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "RequestDate",
                table: "Projects",
                newName: "StartDate");
        }
    }
}
