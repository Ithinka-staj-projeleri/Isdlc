using System.Text.Json;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// Cross tests for the company's own request fields (Faz 7 · A6, written by Baran for Dide's work; A10). They try what
/// CustomFieldServiceTests leaves out: two companies with the same key, a value sent for another company's field, a
/// required field that was switched off, what an update can change, who may see inactive fields, and a skip rule on a
/// field that was switched off. Each test opens its own companies. Expectations: docs/configurability.md → Bölüm 5, 6.
/// </summary>
[Collection("Database")]
public class CustomFieldCrossTests(DatabaseFixture database)
{
    private sealed record Company(int TenantId, int AdminId, int MemberId);

    private async Task<Company> OpenCompanyAsync()
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

        var admin = await TestUsers.CreateAsync(
            database, null, permission: Permission.Analysis, tenantId: tenantId, isTenantAdmin: true);
        var member = await TestUsers.CreateAsync(database, null, permission: Permission.Analysis, tenantId: tenantId);

        await using (var scope = database.CreateScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
            await transaction.CommitAsync();
        }

        return new Company(tenantId, admin.Id, member.Id);
    }

    private async Task<T> FieldsAsync<T>(Company company, int userId, Func<ICustomFieldService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var service = new CustomFieldService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId, company.TenantId, isTenantAdmin: userId == company.AdminId));
        return await action(service);
    }

    private async Task<T> ProjectsAsync<T>(Company company, Func<IProjectService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var service = new ProjectService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true),
            new RecordingNotificationService());
        return await action(service);
    }

    private Task<CustomFieldDto> AddAsync(Company company, CreateCustomFieldDto request) =>
        FieldsAsync(company, company.AdminId, s => s.CreateAsync(request, CancellationToken.None));

    private Task<CustomFieldDto> UpdateAsync(
        Company company, CustomFieldDto field, Func<UpdateCustomFieldDto, UpdateCustomFieldDto> change)
    {
        var request = change(new UpdateCustomFieldDto
        {
            Label = field.Label, Options = [.. field.Options], IsRequired = field.IsRequired, HelpText = field.HelpText,
            Order = field.Order, IsActive = field.IsActive,
        });
        return FieldsAsync(company, company.AdminId, s => s.UpdateAsync(field.Id, request, CancellationToken.None));
    }

    private static CreateCustomFieldDto Region(bool isRequired = false) => new()
    {
        Key = "bolge", Label = "Bölge", Type = CustomFieldType.Select, Options = ["Marmara", "Ege"], IsRequired = isRequired,
    };

    private static CreateProjectDto Request(string? customJson = null) => CreateProjectDtoTests.Valid() with
    {
        BusinessAnalystId = null,
        ProjectManagerId = null,
        CustomValues = customJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(customJson),
    };

    // ---- Two companies ----

    [Fact]
    public async Task Create_SameKeyInAnotherCompany_IsAllowedAndEachSeesItsOwn()
    {
        // The key is unique within a company (TenantId, Key), not across companies.
        var first = await OpenCompanyAsync();
        var second = await OpenCompanyAsync();

        var mine = await AddAsync(first, Region());
        var theirs = await AddAsync(second, Region() with { Options = ["İç Anadolu"] });

        Assert.Equal("bolge", theirs.Key);
        var firstList = await FieldsAsync(first, first.MemberId, s => s.ListAsync(false, CancellationToken.None));
        Assert.Equal(["Marmara", "Ege"], Assert.Single(firstList, f => f.Id == mine.Id).Options);
        Assert.DoesNotContain(firstList, f => f.Id == theirs.Id);
    }

    [Fact]
    public async Task CreateProject_ValueForAnotherCompanysField_IsNotSaved()
    {
        // Company B has no "bolge" field: the value is not a field of B, so it is dropped like any unknown key.
        var withField = await OpenCompanyAsync();
        var withoutField = await OpenCompanyAsync();
        await AddAsync(withField, Region());

        var project = await ProjectsAsync(withoutField, s =>
            s.CreateAsync(Request("""{ "bolge": "Ege" }"""), CancellationToken.None));

        Assert.Empty(project.CustomValues);
    }

    // ---- Switching a field off ----

    [Fact]
    public async Task CreateProject_RequiredFieldSwitchedOff_IsNotAsked()
    {
        // Only a required field that is active is required (docs/configurability.md → Uç noktalar).
        var company = await OpenCompanyAsync();
        var field = await AddAsync(company, Region(isRequired: true));
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            ProjectsAsync(company, s => s.CreateAsync(Request(), CancellationToken.None)));

        await UpdateAsync(company, field, r => r with { IsActive = false });
        var project = await ProjectsAsync(company, s => s.CreateAsync(Request(), CancellationToken.None));

        Assert.Empty(project.CustomValues);
    }

    [Fact]
    public async Task List_InactiveFields_OnlyForTheAdministrator()
    {
        var company = await OpenCompanyAsync();
        var field = await AddAsync(company, Region());
        await UpdateAsync(company, field, r => r with { IsActive = false });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            FieldsAsync(company, company.MemberId, s => s.ListAsync(true, CancellationToken.None)));
        Assert.Empty(await FieldsAsync(company, company.MemberId, s => s.ListAsync(false, CancellationToken.None)));
        Assert.Single(await FieldsAsync(company, company.AdminId, s => s.ListAsync(true, CancellationToken.None)));
    }

    [Fact]
    public async Task Update_KeyAndTypeNeverChange()
    {
        var company = await OpenCompanyAsync();
        var field = await AddAsync(company, Region());

        var updated = await UpdateAsync(company, field, r => r with { Label = "Satış bölgesi", Options = ["Marmara", "Ege", "Akdeniz"] });

        Assert.Equal(("bolge", CustomFieldType.Select, "Satış bölgesi"), (updated.Key, updated.Type, updated.Label));
        Assert.Equal(["Marmara", "Ege", "Akdeniz"], updated.Options);
    }

    [Fact]
    public async Task Update_OptionUsedInARule_CanChangeOnlyItsLetterCase()
    {
        // Rules compare without letter case, so "Ege" → "EGE" keeps the rule working and is allowed; dropping it is not.
        var company = await OpenCompanyAsync();
        var field = await AddAsync(company, Region());
        await SetFirstStepRuleAsync(company, new StepConditionRule("custom:bolge", ConditionOperator.Equals, "Ege"));

        var renamed = await UpdateAsync(company, field, r => r with { Options = ["Marmara", "EGE"] });
        await Assert.ThrowsAsync<ConflictException>(() => UpdateAsync(company, renamed, r => r with { Options = ["Marmara"] }));

        var project = await ProjectsAsync(company, s => s.CreateAsync(Request("""{ "bolge": "EGE" }"""), CancellationToken.None));
        Assert.Equal("1", project.CurrentStep!.Code);
    }

    // Found by this cross test: StepConditions reads the stored value (CustomValueRules.ToText) without asking whether the
    // field is still active, so the rule still holds. Waiting for the fix (A5 rules · A6 fields); remove Skip then.
    [Fact(Skip = "Pasif özel alana bakan kural hâlâ sağlanıyor; düzeltme bekleniyor (A5/A6).")]
    public async Task SkipRule_OnAFieldSwitchedOff_IsNotMet()
    {
        // "Pasif özel alana ya da artık olmayan bir seçeneğe bakan kural sağlanmamış sayılır" (docs/configurability.md,
        // Bölüm 5). The project keeps the value of the field it was opened with; once the field is switched off, the rule
        // on it must not hold any more, so step 1 is skipped for the project.
        var company = await OpenCompanyAsync();
        var field = await AddAsync(company, Region());
        var stepId = await SetFirstStepRuleAsync(company, new StepConditionRule("custom:bolge", ConditionOperator.Equals, "Ege"));
        var project = await ProjectsAsync(company, s => s.CreateAsync(Request("""{ "bolge": "Ege" }"""), CancellationToken.None));
        Assert.DoesNotContain(stepId, (await ProcessAsync(company, project.Id)).SkippedStepIds);

        await UpdateAsync(company, field, r => r with { IsActive = false });

        Assert.Contains(stepId, (await ProcessAsync(company, project.Id)).SkippedStepIds);
    }

    /// <summary>Puts the rule on step 1 of the company's new request flow; returns the step's id.</summary>
    private async Task<int> SetFirstStepRuleAsync(Company company, StepConditionRule rule)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var first = await db.ProcessSteps.Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
            .OrderBy(s => s.Order).FirstAsync();
        first.Conditions = [rule];
        await db.SaveChangesAsync();
        return first.Id;
    }

    private async Task<Isdlc.Application.Process.Dtos.ProjectProcessDto> ProcessAsync(Company company, int projectId)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var process = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true),
            new RecordingNotificationService(),
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await process.GetProjectProcessAsync(projectId, CancellationToken.None);
    }
}
