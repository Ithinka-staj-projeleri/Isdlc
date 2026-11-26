using Isdlc.Domain.Identity;

namespace Isdlc.Application.Tests.Common;

/// <summary>
/// The second company of the test database, next to Ithinka: Deneme A.Ş. gets the templates at startup like any company
/// and has its own users (docs/multi-tenancy.md → Testler). Its id: DatabaseFixture.DenemeId.
/// Tests that change data still create their own records (TestProjects.CreateAsync(..., tenantId: database.DenemeId)).
/// </summary>
public static class TestTenants
{
    public const string DenemeCode = "deneme";
    public const string DenemeName = "Deneme A.Ş.";

    public const string DenemeAnalyst = "analist@deneme.local";               // Analysis
    public const string DenemeManager = "yonetici@deneme.local";              // Approval
    public const string DenemeProjectManager = "proje.yoneticisi@deneme.local"; // ProjectManagement
    public const string DenemeDeveloper = "gelistirici@deneme.local";         // Development

    internal static readonly (string Email, string FullName, Permission Permission)[] DenemeUsers =
    [
        (DenemeAnalyst, "Deniz Analist", Permission.Analysis),
        (DenemeManager, "Derya Yönetici", Permission.Approval),
        (DenemeProjectManager, "Doruk Proje", Permission.ProjectManagement),
        (DenemeDeveloper, "Duru Geliştirici", Permission.Development),
    ];
}
