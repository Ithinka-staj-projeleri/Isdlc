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
using Isdlc.Application.Tests.Identity;
using Isdlc.Application.Tests.Projects;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// Faz 7 · A10 separation: what one company's administrator configures (flow, request fields, permissions) never reaches
/// another company. Both companies are opened by the test with the templates, so their flows share codes and step ids
/// differ; Ithinka's and Deneme's configuration is never changed.
/// Rules: docs/configurability.md, docs/multi-tenancy.md.
/// </summary>
[Collection("Database")]
public class ConfigurabilityIsolationTests(DatabaseFixture database)
{
    private const string Flow = ProcessFlowCodes.NewRequest;

    private sealed record Company(int TenantId, int AdminId, int AnalystId);

    private sealed class NoCache : IAccountStatusCache
    {
        public void Forget(int userId, int? tenantId) { }
    }

    private async Task<Company> OpenCompanyAsync()
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant { Code = $"ayrim-{Guid.NewGuid():N}"[..20], Name = "Ayrım A.Ş.", CreatedAt = DateTime.UtcNow };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        await using (var scope = database.CreateScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
            await transaction.CommitAsync();
        }

        var admin = await TestUsers.CreateAsync(database, null, permission: Permission.Analysis, tenantId: tenantId, isTenantAdmin: true);
        var analyst = await TestUsers.CreateAsync(database, null, permission: Permission.Analysis, tenantId: tenantId);
        return new Company(tenantId, admin.Id, analyst.Id);
    }

    private async Task<T> FlowsAsync<T>(Company company, Func<IProcessFlowEditService, IProcessService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true);
        var process = new ProcessService(db, user, new RecordingNotificationService(), TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await action(new ProcessFlowEditService(db, user, process), process);
    }

    private Task<ProcessFlowDto> FlowAsync(Company company) =>
        FlowsAsync(company, (_, process) => process.GetFlowAsync(Flow, includeInactive: true, default));

    private async Task<T> FieldsAsync<T>(Company company, Func<ICustomFieldService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        return await action(new CustomFieldService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true)));
    }

    private async Task<ProjectDto> OpenRequestAsync(Company company)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var service = new ProjectService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true), new RecordingNotificationService());
        return await service.CreateAsync(CreateProjectDtoTests.Valid() with { BusinessAnalystId = null, ProjectManagerId = null }, default);
    }

    private async Task<T> UsersAsync<T>(Company company, Func<IUserAdminService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        return await action(new UserAdminService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            new NoCache(),
            TimeProvider.System));
    }

    private static IReadOnlyList<ProcessStepDto> Steps(ProcessFlowDto flow) => flow.Stages.SelectMany(s => s.Steps).ToList();

    private static CreateCustomFieldDto Region() => new()
    {
        Key = "bolge", Label = "Bölge", Type = CustomFieldType.Select, Options = ["Marmara", "Ege"], IsRequired = true,
    };

    // ---- Flow ----

    [Fact]
    public async Task FlowEdit_InOneCompany_TheOthersFlowWithTheSameCode_IsUnchanged()
    {
        var a = await OpenCompanyAsync();
        var b = await OpenCompanyAsync();
        var before = await FlowAsync(b);

        var flow = await FlowAsync(a);
        var second = Steps(flow).Single(s => s.Code == "2");
        flow = await FlowsAsync(a, (edit, _) => edit.AddStepAsync(Flow, new AddStepDto
        {
            RowVersion = flow.RowVersion, StageId = flow.Stages[0].Id, AfterStepId = null,
            Text = "Yalnızca A'nın adımı", ResponsiblePermission = Permission.Analysis,
        }, default));
        flow = await FlowsAsync(a, (edit, _) => edit.SetStepActiveAsync(
            second.Id, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default));
        await FlowsAsync(a, (edit, _) => edit.UpdateFlowAsync(
            Flow, new UpdateFlowDto { RowVersion = flow.RowVersion, Name = "A'nın talep akışı", UsedFor = ProcessFlowUse.NewRequest }, default));

        var after = await FlowAsync(b);

        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.False(after.IsCustomized);
        Assert.Equal(Steps(before).Select(s => (s.Id, s.Code, s.Text, s.IsActive)), Steps(after).Select(s => (s.Id, s.Code, s.Text, s.IsActive)));
        Assert.DoesNotContain(Steps(after), s => s.Text == "Yalnızca A'nın adımı");
    }

    [Fact]
    public async Task FlowEdit_TheOtherCompanysStageOrStep_IsNotFound()
    {
        var a = await OpenCompanyAsync();
        var b = await OpenCompanyAsync();
        var flowA = await FlowAsync(a);
        var flowB = await FlowAsync(b);
        var stepB = Steps(flowB).Single(s => s.Code == "2");

        await Assert.ThrowsAsync<NotFoundException>(() => FlowsAsync(a, (edit, _) => edit.SetStepActiveAsync(
            stepB.Id, false, new FlowVersionDto { RowVersion = flowA.RowVersion }, default)));
        await Assert.ThrowsAsync<NotFoundException>(() => FlowsAsync(a, (edit, _) => edit.UpdateStageAsync(
            flowB.Stages[0].Id, new SaveStageDto { RowVersion = flowB.RowVersion, Title = "X", ShortTitle = "X" }, default)));

        Assert.True(Steps(await FlowAsync(b)).Single(s => s.Id == stepB.Id).IsActive);
    }

    [Fact]
    public async Task FlowEdit_ARequestOfTheOtherCompany_StartsOnItsOwnFirstStep()
    {
        // A deactivates its first step; B's new request still starts on step 1 of B's own flow.
        var a = await OpenCompanyAsync();
        var b = await OpenCompanyAsync();
        var flowA = await FlowAsync(a);
        await FlowsAsync(a, (edit, _) => edit.SetStepActiveAsync(
            Steps(flowA).Single(s => s.Code == "1").Id, false, new FlowVersionDto { RowVersion = flowA.RowVersion }, default));
        var firstOfB = Steps(await FlowAsync(b)).Single(s => s.Code == "1");

        var project = await OpenRequestAsync(b);
        var process = await FlowsAsync(b, (_, p) => p.GetProjectProcessAsync(project.Id, default));

        Assert.Equal(firstOfB.Id, process.CurrentStep!.Id);
    }

    // ---- Request fields ----

    [Fact]
    public async Task CustomField_RequiredInOneCompany_TheOtherOpensRequestsWithoutIt()
    {
        var a = await OpenCompanyAsync();
        var b = await OpenCompanyAsync();
        await FieldsAsync(a, s => s.CreateAsync(Region(), default));

        await Assert.ThrowsAsync<FieldValidationException>(() => OpenRequestAsync(a));
        var project = await OpenRequestAsync(b);

        Assert.Empty(project.CustomValues);
        Assert.Empty(await FieldsAsync(b, s => s.ListAsync(true, default)));
    }

    [Fact]
    public async Task CustomField_SameKeyInBothCompanies_IsAllowed_AndEachKeepsItsOwn()
    {
        var a = await OpenCompanyAsync();
        var b = await OpenCompanyAsync();
        var fieldA = await FieldsAsync(a, s => s.CreateAsync(Region(), default));

        var fieldB = await FieldsAsync(b, s => s.CreateAsync(
            new CreateCustomFieldDto { Key = "bolge", Label = "Bölge kodu", Type = CustomFieldType.Text }, default));

        Assert.NotEqual(fieldA.Id, fieldB.Id);
        Assert.Equal(CustomFieldType.Select, Assert.Single(await FieldsAsync(a, s => s.ListAsync(true, default))).Type);
        Assert.Equal(CustomFieldType.Text, Assert.Single(await FieldsAsync(b, s => s.ListAsync(true, default))).Type);
        await Assert.ThrowsAsync<NotFoundException>(() => FieldsAsync(a, s => s.UpdateAsync(
            fieldB.Id, new UpdateCustomFieldDto { Label = "Değişti", IsActive = false }, default)));
    }

    // ---- Permissions ----

    [Fact]
    public async Task Permissions_TheOtherCompanysUser_CannotBeChanged_NorListed()
    {
        var a = await OpenCompanyAsync();
        var b = await OpenCompanyAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => UsersAsync(a, s => s.UpdateAsync(b.AnalystId, new UpdateUserDto
        {
            FullName = "Değişti", Permissions = [Permission.Approval, Permission.Sales], IsActive = true,
        }, default)));
        var listA = await UsersAsync(a, s => s.ListAsync(default));
        var analystB = Assert.Single(await UsersAsync(b, s => s.ListAsync(default)), u => u.Id == b.AnalystId);

        Assert.DoesNotContain(listA, u => u.Id == b.AnalystId || u.Id == b.AdminId);
        Assert.Equal([Permission.Analysis], analystB.Permissions);
        Assert.NotEqual("Değişti", analystB.FullName);
    }
}
