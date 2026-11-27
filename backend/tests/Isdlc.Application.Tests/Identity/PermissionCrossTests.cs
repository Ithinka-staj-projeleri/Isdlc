using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Projects;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// Cross tests (PLANNING.md): Erva → Baran's A2, titles replaced with permissions. What A2's own tests leave between them:
/// a person with several permissions, a permission taken away, a new company's administrator who starts without any,
/// the permission rows across companies and an inactive user.
/// Rules: docs/configurability.md → Yetkiler.
/// </summary>
[Collection("Database")]
public class PermissionCrossTests(DatabaseFixture database)
{
    private const string Temporary = "Gecici-Parola-2026";

    private sealed class NoCache : IAccountStatusCache
    {
        public void Forget(int userId, int? tenantId) { }
    }

    private async Task<T> ProcessAsync<T>(int userId, Func<IProcessService, Task<T>> action, int tenantId = Tenant.IthinkaId)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId, tenantId),
            new RecordingNotificationService(),
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await action(service);
    }

    private async Task<T> AdminAsync<T>(int adminId, Func<IUserAdminService, Task<T>> action, int tenantId = Tenant.IthinkaId)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new UserAdminService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(adminId, tenantId, isTenantAdmin: true),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            new NoCache(),
            TimeProvider.System);
        return await action(service);
    }

    private Task<int> Step(string code, int? tenantId = null) => TestProjects.StepIdAsync(database, code, tenantId: tenantId);

    private static UpdateUserDto Keep(User user, List<Permission> permissions) => new()
    {
        FullName = user.FullName, Permissions = permissions, IsActive = user.IsActive, IsTenantAdmin = user.IsTenantAdmin,
    };

    // ---- Several permissions ----

    [Fact]
    public async Task SeveralPermissions_OnePerson_DoesTheAnalysisStepAndApprovesTheOffer()
    {
        // One person can hold what used to be two titles: analyst and department manager.
        var user = await TestUsers.CreateAsync(database, null, permission: null, permissions: [Permission.Analysis, Permission.Approval]);
        var analysisProject = await TestProjects.CreateAsync(database, "2");
        var offerProject = await TestProjects.CreateAsync(database, "10");

        var afterAnalysis = await ProcessAsync(user.Id, async s =>
            await s.CompleteStepAsync(analysisProject, new CompleteStepDto { ExpectedStepId = await Step("2") }, default));
        var afterApproval = await ProcessAsync(user.Id, async s =>
            await s.AnswerStepAsync(offerProject, new AnswerStepDto { ExpectedStepId = await Step("10"), Answer = StepAnswer.Yes }, default));

        Assert.Equal("3", afterAnalysis.CurrentStep!.Code);
        Assert.Equal("11", afterApproval.CurrentStep!.Code);
    }

    [Fact]
    public async Task SeveralPermissions_WithoutTheApprovers_TheOfferStepNamesApproval()
    {
        // Step 10's responsible is Sales but Approval closes it: holding Sales and Analysis is not enough.
        var user = await TestUsers.CreateAsync(database, null, permission: null, permissions: [Permission.Analysis, Permission.Sales]);
        var project = await TestProjects.CreateAsync(database, "10");

        var process = await ProcessAsync(user.Id, s => s.GetProjectProcessAsync(project, default));

        Assert.False(process.AvailableActions.CanAnswer);
        Assert.Equal(Permission.Approval, process.AvailableActions.MissingPermission);
        Assert.StartsWith("Bu adımı onaylamak için «Onay ve karar» yetkisi gerekir.", process.AvailableActions.BlockedReason);
    }

    // ---- A permission taken away ----

    [Fact]
    public async Task PermissionTakenAway_TheNextRequestIsRefused()
    {
        var admin = await TestUsers.CreateAsync(database, Temporary, permission: null, isTenantAdmin: true);
        var user = await TestUsers.CreateAsync(database, null, permission: Permission.Analysis);
        var project = await TestProjects.CreateAsync(database, "2");

        await AdminAsync(admin.Id, s => s.UpdateAsync(user.Id, Keep(user, []), default));

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => ProcessAsync(user.Id, async s =>
            await s.CompleteStepAsync(project, new CompleteStepDto { ExpectedStepId = await Step("2") }, default)));
        Assert.StartsWith("Bu adım için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin: ", error.Message);
        Assert.Contains(admin.FullName, error.Message);
    }

    [Fact]
    public async Task InactiveUser_KeepsThePermissionRows_ButCannotAct()
    {
        var user = await TestUsers.CreateAsync(database, null, isActive: false, permission: Permission.Analysis);
        var project = await TestProjects.CreateAsync(database, "2");

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => ProcessAsync(user.Id, async s =>
            await s.CompleteStepAsync(project, new CompleteStepDto { ExpectedStepId = await Step("2") }, default)));

        Assert.Equal("İşlem yapmak için giriş yapılmalıdır.", error.Message);
    }

    // ---- A new company's administrator ----

    [Fact]
    public async Task NewCompanyAdministrator_StartsWithoutPermissions_GivesThemselvesAnalysis_ThenOpensARequest()
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant { Code = $"capraz-{Guid.NewGuid():N}"[..20], Name = "Çapraz A.Ş.", CreatedAt = DateTime.UtcNow };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        await using (var scope = database.CreateScope(tenantId))
        {
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
        }

        var admin = await TestUsers.CreateAsync(database, Temporary, permission: null, tenantId: tenantId, isTenantAdmin: true,
            fullName: "Deniz Yönetici");

        async Task<ProjectDto> OpenAsync()
        {
            await using var scope = database.CreateScope(tenantId);
            var service = new ProjectService(
                scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                new TestUser(admin.Id, tenantId), new RecordingNotificationService());
            return await service.CreateAsync(CreateProjectDtoTests.Valid() with { BusinessAnalystId = admin.Id }, default);
        }

        // Without a permission the administrator is told whom to ask: themselves.
        var refused = await Assert.ThrowsAsync<ForbiddenException>(OpenAsync);
        Assert.Equal("Talep açmak için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin: Deniz Yönetici.",
            refused.Message);

        await AdminAsync(admin.Id, s => s.UpdateAsync(admin.Id, Keep(admin, [Permission.Analysis]), default), tenantId);
        var project = await OpenAsync();

        Assert.Equal("1", project.Code);
    }

    // ---- Permission rows across companies ----

    [Fact]
    public async Task PermissionRows_AreTheUsersCompanys_AndInvisibleToAnother()
    {
        var denemeUser = await TestUsers.CreateAsync(database, null, permission: Permission.Testing, tenantId: database.DenemeId);

        await using var ithinka = database.CreateScope();
        var ithinkaDb = ithinka.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.False(await ithinkaDb.UserPermissions.AnyAsync(p => p.UserId == denemeUser.Id));
        Assert.Null(await ithinkaDb.LoadAccessAsync(denemeUser.Id, default));

        await using var deneme = database.CreateScope(database.DenemeId);
        var row = await deneme.ServiceProvider.GetRequiredService<IApplicationDbContext>().UserPermissions
            .SingleAsync(p => p.UserId == denemeUser.Id);
        Assert.Equal(database.DenemeId, row.TenantId);
        Assert.Equal(Permission.Testing, row.Permission);
    }

    [Fact]
    public async Task UserList_ByPermission_OnlyTheCompanysPeople()
    {
        var ithinkaAnalyst = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var denemeAnalyst = await TestProjects.UserIdAsync(database, TestTenants.DenemeAnalyst, database.DenemeId);

        await using var scope = database.CreateScope(database.DenemeId);
        var analysts = await new UserService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>())
            .ListActiveAsync(Permission.Analysis, default);

        Assert.Contains(analysts, u => u.Id == denemeAnalyst);
        Assert.DoesNotContain(analysts, u => u.Id == ithinkaAnalyst);
    }
}
