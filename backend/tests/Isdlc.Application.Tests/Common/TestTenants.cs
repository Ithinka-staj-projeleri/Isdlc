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

    public const string DenemeAnalyst = "analist@deneme.local";               // BusinessAnalyst
    public const string DenemeManager = "yonetici@deneme.local";              // SoftwareDepartmentManager
    public const string DenemeProjectManager = "proje.yoneticisi@deneme.local"; // ProjectManager
    public const string DenemeDeveloper = "gelistirici@deneme.local";         // SoftwareDeveloper

    internal static readonly (string Email, string FullName, UserRole Role)[] DenemeUsers =
    [
        (DenemeAnalyst, "Deniz Analist", UserRole.BusinessAnalyst),
        (DenemeManager, "Derya Yönetici", UserRole.SoftwareDepartmentManager),
        (DenemeProjectManager, "Doruk Proje", UserRole.ProjectManager),
        (DenemeDeveloper, "Duru Geliştirici", UserRole.SoftwareDeveloper),
    ];
}
