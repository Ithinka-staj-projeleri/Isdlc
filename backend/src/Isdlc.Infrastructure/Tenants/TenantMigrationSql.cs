using Isdlc.Domain.Tenants;

namespace Isdlc.Infrastructure.Tenants;

/// <summary>
/// The hand-written steps of the AddTenants migration, which moves every record of before Phase 6 to Ithinka
/// (docs/multi-tenancy.md → Mevcut verinin taşınması). Kept here so the migration can be generated again and only these
/// three lines added to it:
///   1. every TenantId column is added with "defaultValue: Tenant.IthinkaId" (existing rows become Ithinka's);
///   2. right after the Tenants table is created: migrationBuilder.Sql(TenantMigrationSql.InsertIthinka);
///   3. at the end of Up: migrationBuilder.Sql(TenantMigrationSql.AfterColumns);
/// </summary>
public static class TenantMigrationSql
{
    /// <summary>Ithinka with a fixed Id, before any foreign key points to it. AI stays on, as it is today.</summary>
    public static readonly string InsertIthinka = $"""
        SET IDENTITY_INSERT [Tenants] ON;
        INSERT INTO [Tenants] ([Id], [Code], [Name], [IsActive], [AiEnabled], [AiDataSharingAllowed], [CreatedAt])
        VALUES ({Tenant.IthinkaId}, N'ithinka', N'Ithinka', 1, 1, 1, SYSUTCDATETIME());
        SET IDENTITY_INSERT [Tenants] OFF;
        """;

    /// <summary>
    /// Users (TenantId may be empty, so no default) become Ithinka's; the existing copies of the templates get their
    /// template code; the temporary defaults are dropped, so a TenantId is always written by the application.
    /// </summary>
    public static readonly string AfterColumns = $"""
        UPDATE [Users] SET [TenantId] = {Tenant.IthinkaId} WHERE [TenantId] IS NULL;
        UPDATE [ProcessFlows] SET [TemplateCode] = [Code];
        UPDATE [DocumentTypes] SET [TemplateCode] = [Code];
        UPDATE [QuestionSets] SET [TemplateCode] = [Code];

        DECLARE @drop nvarchar(max) = N'';
        SELECT @drop += N'ALTER TABLE ' + QUOTENAME(OBJECT_NAME(dc.parent_object_id))
            + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE c.name = N'TenantId';
        EXEC sp_executesql @drop;
        """;
}
