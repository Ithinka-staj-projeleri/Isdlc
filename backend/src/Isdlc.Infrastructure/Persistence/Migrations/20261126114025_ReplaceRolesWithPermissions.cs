using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdlc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceRolesWithPermissions : Migration
    {
        /// <inheritdoc />
        // Faz 7 · A2: titles become permissions (docs/configurability.md → Bugünkü verinin taşınması).
        // One table maps both ways; nobody loses what they could do, and every step keeps who does it.
        private static readonly (string Role, string Permission)[] Mapping =
        [
            ("BusinessAnalyst", "Analysis"),
            ("SoftwareDepartmentManager", "Approval"),
            ("ProjectManager", "ProjectManagement"),
            ("SalesRepresentative", "Sales"),
            ("SoftwareDeveloper", "Development"),
            ("TestSpecialist", "Testing"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_TenantId",
                table: "UserPermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_Permission",
                table: "UserPermissions",
                columns: new[] { "UserId", "Permission" },
                unique: true);

            // Each company user gets the permission of their title, in their own company; the Platform Administrator
            // becomes the mark and gets none.
            foreach (var (role, permission) in Mapping)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO [UserPermissions] ([TenantId], [UserId], [Permission])
                    SELECT [TenantId], [Id], N'{permission}' FROM [Users]
                    WHERE [Role] = N'{role}' AND [TenantId] IS NOT NULL;
                    """);
            }

            migrationBuilder.Sql("UPDATE [Users] SET [IsPlatformAdmin] = 1 WHERE [Role] = N'PlatformAdmin';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_TenantByRole",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_PlatformAdmin",
                table: "Users",
                sql: "([IsPlatformAdmin] = 1 AND [TenantId] IS NULL AND [IsTenantAdmin] = 0) OR ([IsPlatformAdmin] = 0 AND [TenantId] IS NOT NULL)");

            // Every company's step copies: the same column, the title's permission in it.
            migrationBuilder.RenameColumn(
                name: "ResponsibleRole",
                table: "ProcessSteps",
                newName: "ResponsiblePermission");

            migrationBuilder.RenameColumn(
                name: "ApproverRole",
                table: "ProcessSteps",
                newName: "ApproverPermission");

            foreach (var (role, permission) in Mapping)
            {
                migrationBuilder.Sql($"""
                    UPDATE [ProcessSteps] SET [ResponsiblePermission] = N'{permission}' WHERE [ResponsiblePermission] = N'{role}';
                    UPDATE [ProcessSteps] SET [ApproverPermission] = N'{permission}' WHERE [ApproverPermission] = N'{role}';
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to one title per user: the first of their permissions in the mapping's order (a user without one
            // becomes a business analyst); the Platform Administrator gets its title back.
            foreach (var (role, permission) in Mapping)
            {
                migrationBuilder.Sql($"""
                    UPDATE [ProcessSteps] SET [ResponsiblePermission] = N'{role}' WHERE [ResponsiblePermission] = N'{permission}';
                    UPDATE [ProcessSteps] SET [ApproverPermission] = N'{role}' WHERE [ApproverPermission] = N'{permission}';
                    """);
            }

            migrationBuilder.RenameColumn(
                name: "ResponsiblePermission",
                table: "ProcessSteps",
                newName: "ResponsibleRole");

            migrationBuilder.RenameColumn(
                name: "ApproverPermission",
                table: "ProcessSteps",
                newName: "ApproverRole");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_PlatformAdmin",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "BusinessAnalyst");

            foreach (var (role, permission) in Mapping.Reverse())
            {
                migrationBuilder.Sql($"""
                    UPDATE [Users] SET [Role] = N'{role}'
                    WHERE [Id] IN (SELECT [UserId] FROM [UserPermissions] WHERE [Permission] = N'{permission}');
                    """);
            }

            migrationBuilder.Sql("UPDATE [Users] SET [Role] = N'PlatformAdmin' WHERE [IsPlatformAdmin] = 1;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_TenantByRole",
                table: "Users",
                sql: "([Role] = 'PlatformAdmin' AND [TenantId] IS NULL AND [IsTenantAdmin] = 0) OR ([Role] <> 'PlatformAdmin' AND [TenantId] IS NOT NULL)");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                table: "Users");
        }
    }
}
