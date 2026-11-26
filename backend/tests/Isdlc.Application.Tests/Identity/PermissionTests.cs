using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Tenants;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// Faz 7 · A2: permissions in place of titles. The messages and lists are pure; the rest runs on a real database with users
/// made by these tests, so seed users are never changed. Contract: docs/configurability.md → Yetkiler.
/// </summary>
[Collection("Database")]
public class PermissionTests(DatabaseFixture database)
{
    // ---- Messages (karar 2b) ----

    [Fact]
    public void Messages_NameThePermissionAndTheAdministrators()
    {
        string[] admins = ["Deniz Aydın", "Mert Kaya"];

        Assert.Equal("Bu adım için «Test» yetkisi gerekir. Şirket yöneticinizden isteyin: Deniz Aydın, Mert Kaya.",
            PermissionMessages.ForStep(Permission.Testing, admins));
        Assert.Equal("Bu adımı onaylamak için «Onay ve karar» yetkisi gerekir. Şirket yöneticinizden isteyin: Deniz Aydın, Mert Kaya.",
            PermissionMessages.ForApproval(admins));
        Assert.Equal("Talep hakkında karar vermek için «Onay ve karar» yetkisi gerekir. Şirket yöneticinizden isteyin: Deniz Aydın, Mert Kaya.",
            PermissionMessages.ForDecision(admins));
        Assert.Equal("Talep açmak için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin: Deniz Aydın, Mert Kaya.",
            PermissionMessages.ForNewRequest(admins));
    }

    [Fact]
    public void Messages_CompanyWithoutAdministrators_EndsTheSentence()
    {
        Assert.Equal("Talep açmak için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin.",
            PermissionMessages.ForNewRequest([]));
    }

    [Theory]
    [InlineData(Permission.Analysis, "Seçilen kişinin «Analiz adımları» yetkisi yok.")]
    [InlineData(Permission.ProjectManagement, "Seçilen kişinin «Proje yönetimi adımları» yetkisi yok.")]
    public void Messages_SelectedUserLacks(Permission permission, string expected)
    {
        Assert.Equal(expected, PermissionMessages.SelectedUserLacks(permission));
    }

    // ---- The list and the ready-made sets ----

    [Fact]
    public void Catalog_HasEveryPermissionWithItsNameAndOneSetPerFormerTitle()
    {
        var catalog = PermissionCatalogDto.Build();

        Assert.Equal(
            [
                (Permission.Analysis, "Analiz adımları"),
                (Permission.ProjectManagement, "Proje yönetimi adımları"),
                (Permission.Approval, "Onay ve karar"),
                (Permission.Sales, "Teklif ve anlaşma"),
                (Permission.Development, "Geliştirme"),
                (Permission.Testing, "Test"),
            ],
            catalog.Permissions.Select(p => (p.Value, p.Name)));
        Assert.Equal(
            [
                ("İş Analisti", Permission.Analysis),
                ("Yazılım Departman Yöneticisi", Permission.Approval),
                ("Proje Yöneticisi", Permission.ProjectManagement),
                ("Satış Temsilcisi", Permission.Sales),
                ("Yazılım Geliştirici", Permission.Development),
                ("Test Uzmanı", Permission.Testing),
            ],
            catalog.Presets.Select(p => (p.Name, Assert.Single(p.Permissions))));
    }

    // ---- What the seed and the migration left ----

    [Theory]
    [InlineData(TestProjects.Analyst, Permission.Analysis)]
    [InlineData(TestProjects.Manager, Permission.Approval)]
    [InlineData(TestProjects.ProjectManager, Permission.ProjectManagement)]
    [InlineData(TestProjects.Developer, Permission.Development)]
    [InlineData(TestProjects.Sales, Permission.Sales)]
    [InlineData("burak.sahin@isdlc.local", Permission.Testing)]
    public async Task Seed_EveryUserHasThePermissionOfTheirFormerTitle(string email, Permission expected)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var permissions = await db.UserPermissions.AsNoTracking()
            .Where(p => p.User.Email == email)
            .Select(p => p.Permission)
            .ToListAsync();

        Assert.Equal([expected], permissions);
    }

    [Fact]
    public async Task Seed_EveryCompanysStepsHaveTheSamePermissionsAsTheTemplate()
    {
        // The migration and the seed gave every company's copy the same permissions (Ithinka's and Deneme's).
        await using var scope = database.CreateScope();
        var steps = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProcessSteps
            .IgnoreQueryFilters() // tenant: every company's copy is compared
            .Where(s => s.TenantId == Tenant.IthinkaId || s.TenantId == database.DenemeId)
            .Select(s => new { s.TenantId, Flow = s.ProcessFlow.TemplateCode, s.Code, s.ResponsiblePermission, s.ApproverPermission })
            .ToListAsync();

        List<string> Keys(int tenantId) => steps
            .Where(s => s.TenantId == tenantId)
            .Select(s => $"{s.Flow}/{s.Code}:{s.ResponsiblePermission}/{s.ApproverPermission}")
            .Order()
            .ToList();
        var ithinka = Keys(Tenant.IthinkaId);
        var deneme = Keys(database.DenemeId);
        Assert.NotEmpty(ithinka);
        Assert.Equal(ithinka, deneme);
    }

    // ---- Acting in the flow ----

    [Fact]
    public async Task Complete_WithoutThePermission_Is403NamingTheAdministrators()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");
        var tester = await TestUsers.CreateAsync(database, password: null, permission: Permission.Testing);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => CompleteAsync(tester.Id, projectId));

        Assert.StartsWith("Bu adım için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin: ", error.Message);
        Assert.Contains("Mert Kaya", error.Message);
    }

    [Fact]
    public async Task Complete_PermissionGivenByTheAdministrator_CountsFromTheNextRequest()
    {
        // Permissions are not in the token: the administrator's change counts without signing in again.
        var projectId = await TestProjects.CreateAsync(database, "1");
        var user = await TestUsers.CreateAsync(database, password: null, permission: null);
        var admin = await TestUsers.CreateAsync(database, password: null, permission: null, isTenantAdmin: true);
        await Assert.ThrowsAsync<ForbiddenException>(() => CompleteAsync(user.Id, projectId));

        await using (var scope = database.CreateScope())
        {
            var service = new UserAdminService(
                scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                new TestUser(admin.Id, Tenant.IthinkaId, isTenantAdmin: true),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                new NoAccountStatusCache(),
                TimeProvider.System);
            await service.UpdateAsync(user.Id, new UpdateUserDto
            {
                FullName = user.FullName, Permissions = [Permission.Analysis], IsActive = true,
            }, default);
        }

        var result = await CompleteAsync(user.Id, projectId);

        Assert.NotEqual(await TestProjects.StepIdAsync(database, "1"), result.CurrentStep?.Id);
    }

    [Fact]
    public async Task GetProcess_WithoutThePermission_TellsWhichOneIsMissing()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");
        var developer = await TestUsers.CreateAsync(database, password: null, permission: Permission.Development);

        await using var scope = database.CreateScope();
        var process = await new ProcessService(
                scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                new TestUser(developer.Id),
                new RecordingNotificationService(),
                TimeProvider.System)
            .GetProjectProcessAsync(projectId, default);

        Assert.False(process.AvailableActions.CanComplete);
        Assert.Equal(Permission.Analysis, process.AvailableActions.MissingPermission);
        Assert.Contains("Mert Kaya", process.AvailableActions.BlockedReason);
    }

    // ---- The signed-in user and the company ----

    [Fact]
    public async Task GetMe_ReturnsThePermissionsAsTheyAreNow()
    {
        var user = await TestUsers.CreateAsync(
            database, password: null, permissions: [Permission.Testing, Permission.Analysis], isTenantAdmin: true);

        await using var scope = database.CreateScope();
        var me = await new AuthService(
                scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                new UnusedTokenService())
            .GetMeAsync(user.Id);

        Assert.Equal((user.Id, true, false, Tenant.IthinkaId), (me.Id, me.IsTenantAdmin, me.IsPlatformAdmin, me.Tenant?.Id));
        Assert.Equal([Permission.Analysis, Permission.Testing], me.Permissions);
    }

    [Fact]
    public async Task GetMe_PlatformAdmin_HasNoCompanyAndNoPermissions()
    {
        var admin = await TestUsers.CreateAsync(database, password: null, isPlatformAdmin: true);

        await using var scope = database.CreateScope();
        var me = await new AuthService(
                scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                new UnusedTokenService())
            .GetMeAsync(admin.Id);

        Assert.True(me.IsPlatformAdmin);
        Assert.Null(me.Tenant);
        Assert.Empty(me.Permissions);
    }

    [Fact]
    public async Task CurrentTenant_ListsTheActiveAdministrators()
    {
        var inactive = await TestUsers.CreateAsync(
            database, password: null, isTenantAdmin: true, isActive: false, fullName: "Pasif Yönetici");

        await using var scope = database.CreateScope();
        var current = await scope.ServiceProvider.GetRequiredService<ITenantService>().GetCurrentAsync(default);

        Assert.Contains("Mert Kaya", current.Admins);
        Assert.DoesNotContain(inactive.FullName, current.Admins);
    }

    private async Task<ProjectProcessDto> CompleteAsync(int userId, int projectId)
    {
        await using var scope = database.CreateScope();
        var service = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            new RecordingNotificationService(),
            TimeProvider.System);
        return await service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await TestProjects.StepIdAsync(database, "1") }, default);
    }

    private sealed class NoAccountStatusCache : IAccountStatusCache
    {
        public void Forget(int userId, int? tenantId) { }
    }

    private sealed class UnusedTokenService : ITokenService
    {
        public TokenResult Create(User user) => throw new InvalidOperationException("GetMe makes no token.");
    }
}
