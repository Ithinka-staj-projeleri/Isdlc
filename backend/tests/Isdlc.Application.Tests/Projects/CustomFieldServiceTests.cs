using System.Text.Json;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.Process;
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
/// The company's own request fields on a real database (Faz 7 · A6): the definition API and the values on the request.
/// Expectations: docs/configurability.md → Şirkete özel alanlar.
/// A required field would stop every other test from opening requests, so each test opens its own company
/// (with the templates and an analyst who is also its administrator) and works only there.
/// </summary>
[Collection("Database")]
public class CustomFieldServiceTests(DatabaseFixture database)
{
    private sealed record Company(int TenantId, int AdminId, int MemberId);

    private async Task<Company> OpenCompanyAsync()
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant { Code = $"alan-{Guid.NewGuid():N}"[..20], Name = "Alan A.Ş.", CreatedAt = DateTime.UtcNow };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        var admin = await TestUsers.CreateAsync(
            database, null, role: UserRole.BusinessAnalyst, tenantId: tenantId, isTenantAdmin: true);
        var member = await TestUsers.CreateAsync(database, null, role: UserRole.BusinessAnalyst, tenantId: tenantId);

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

    private static CreateCustomFieldDto Region(bool isRequired = false) => new()
    {
        Key = "bolge", Label = "Bölge", Type = CustomFieldType.Select, Options = ["Marmara", "Ege"], IsRequired = isRequired,
    };

    private static UpdateCustomFieldDto Edit(CustomFieldDto field) => new()
    {
        Label = field.Label, Options = [.. field.Options], IsRequired = field.IsRequired, HelpText = field.HelpText,
        Order = field.Order, IsActive = field.IsActive,
    };

    private static CreateProjectDto Request(string? customJson = null) => CreateProjectDtoTests.Valid() with
    {
        BusinessAnalystId = null,
        ProjectManagerId = null,
        CustomValues = customJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(customJson),
    };

    // ---- Definitions ----

    [Fact]
    public async Task Create_Admin_AddsAnActiveFieldAtTheEnd()
    {
        var company = await OpenCompanyAsync();
        await AddAsync(company, new CreateCustomFieldDto { Key = "sozlesme-no", Label = "Sözleşme No", Type = CustomFieldType.Text });

        var region = await AddAsync(company, Region());

        Assert.True(region.IsActive);
        Assert.Equal(["Marmara", "Ege"], region.Options);
        var list = await FieldsAsync(company, company.MemberId, s => s.ListAsync(false, CancellationToken.None));
        Assert.Equal(["sozlesme-no", "bolge"], list.Select(f => f.Key));
        Assert.Equal(list[0].Order + 1, list[1].Order);
    }

    [Fact]
    public async Task CreateAndUpdate_NotAdmin_Forbidden()
    {
        var company = await OpenCompanyAsync();
        var region = await AddAsync(company, Region());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            FieldsAsync(company, company.MemberId, s => s.CreateAsync(Region(), CancellationToken.None)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            FieldsAsync(company, company.MemberId, s => s.UpdateAsync(region.Id, Edit(region), CancellationToken.None)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            FieldsAsync(company, company.MemberId, s => s.ListAsync(true, CancellationToken.None)));
    }

    [Fact]
    public async Task Create_SameKey_Conflict()
    {
        var company = await OpenCompanyAsync();
        await AddAsync(company, Region());

        var error = await Assert.ThrowsAsync<ConflictException>(() => AddAsync(company, Region()));
        Assert.Equal("«bolge» anahtarıyla bir alan zaten var.", error.Message);
    }

    [Fact]
    public async Task Update_Inactive_HiddenFromTheFormButListedForTheAdmin()
    {
        var company = await OpenCompanyAsync();
        var region = await AddAsync(company, Region());

        var updated = await FieldsAsync(company, company.AdminId, s =>
            s.UpdateAsync(region.Id, Edit(region) with { Label = "Satış bölgesi", IsActive = false }, CancellationToken.None));

        Assert.Equal(("bolge", CustomFieldType.Select, "Satış bölgesi", false),
            (updated.Key, updated.Type, updated.Label, updated.IsActive));
        Assert.Empty(await FieldsAsync(company, company.MemberId, s => s.ListAsync(false, CancellationToken.None)));
        Assert.Single(await FieldsAsync(company, company.AdminId, s => s.ListAsync(true, CancellationToken.None)));
    }

    [Fact]
    public async Task Update_OptionsOnATextField_IsAFieldError()
    {
        var company = await OpenCompanyAsync();
        var text = await AddAsync(company, new CreateCustomFieldDto { Key = "not", Label = "Not", Type = CustomFieldType.Text });

        var error = await Assert.ThrowsAsync<FieldValidationException>(() => FieldsAsync(company, company.AdminId, s =>
            s.UpdateAsync(text.Id, Edit(text) with { Options = ["A"] }, CancellationToken.None)));
        Assert.Equal("Seçenekler yalnızca seçim alanında kullanılır.", Assert.Single(error.Errors["options"]));
    }

    [Fact]
    public async Task Update_RemovingAnOptionASkipRuleUses_Conflict_OtherOptionsCanGo()
    {
        var company = await OpenCompanyAsync();
        var region = await AddAsync(company, Region() with { Options = ["Marmara", "Ege", "Akdeniz"] });
        string stepCode;
        await using (var scope = database.CreateScope(company.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var step = await db.ProcessSteps.Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
                .OrderBy(s => s.Order).Skip(3).FirstAsync();
            step.Conditions = [new StepConditionRule("custom:bolge", ConditionOperator.Equals, "Ege")];
            await db.SaveChangesAsync();
            stepCode = step.Code;
        }

        var error = await Assert.ThrowsAsync<ConflictException>(() => FieldsAsync(company, company.AdminId, s =>
            s.UpdateAsync(region.Id, Edit(region) with { Options = ["Marmara", "Akdeniz"] }, CancellationToken.None)));
        Assert.Contains("«Ege» seçeneği bir atlama kuralında kullanılıyor", error.Message);
        Assert.Contains($"{stepCode}. adım", error.Message);

        var updated = await FieldsAsync(company, company.AdminId, s =>
            s.UpdateAsync(region.Id, Edit(region) with { Options = ["Ege", "Karadeniz"] }, CancellationToken.None));
        Assert.Equal(["Ege", "Karadeniz"], updated.Options);
    }

    [Fact]
    public async Task List_AnotherCompanysFields_AreNotSeen()
    {
        var company = await OpenCompanyAsync();
        await AddAsync(company, Region());

        await using var scope = database.CreateScope(database.DenemeId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.False(await db.CustomFields.AnyAsync(f => f.Key == "bolge" && f.TenantId == company.TenantId));
    }

    // ---- Values on the request ----

    [Fact]
    public async Task CreateProject_RequiredFieldMissing_FieldErrorUnderTheField()
    {
        var company = await OpenCompanyAsync();
        await AddAsync(company, Region(isRequired: true));

        var error = await Assert.ThrowsAsync<FieldValidationException>(() =>
            ProjectsAsync(company, s => s.CreateAsync(Request(), CancellationToken.None)));
        Assert.Equal("Bu alan zorunludur.", Assert.Single(error.Errors["customValues.bolge"]));
    }

    [Fact]
    public async Task CreateProject_Values_SavedAndReturned()
    {
        var company = await OpenCompanyAsync();
        await AddAsync(company, Region(isRequired: true));
        await AddAsync(company, new CreateCustomFieldDto { Key = "butce", Label = "Bütçe", Type = CustomFieldType.Number });

        var project = await ProjectsAsync(company, s =>
            s.CreateAsync(Request("""{ "bolge": "ege", "butce": 120000 }"""), CancellationToken.None));

        Assert.Equal("Ege", project.CustomValues["bolge"].GetString());
        Assert.Equal(120000, project.CustomValues["butce"].GetDecimal());
    }

    [Fact]
    public async Task UpdateProject_InactiveFieldsValue_Stays()
    {
        var company = await OpenCompanyAsync();
        var region = await AddAsync(company, Region());
        var project = await ProjectsAsync(company, s =>
            s.CreateAsync(Request("""{ "bolge": "Marmara" }"""), CancellationToken.None));
        await FieldsAsync(company, company.AdminId, s =>
            s.UpdateAsync(region.Id, Edit(region) with { IsActive = false }, CancellationToken.None));

        var updated = await ProjectsAsync(company, s => s.UpdateAsync(project.Id,
            new UpdateProjectDto
            {
                Name = project.Name, Description = project.Description, Source = project.Source,
                RequestChannel = project.RequestChannel, RequestType = project.RequestType,
                CustomerName = project.CustomerName, RequesterName = project.RequesterName,
                RequiresOffer = project.RequiresOffer, RequestDate = project.RequestDate,
                CustomValues = new() { ["bolge"] = JsonSerializer.SerializeToElement("Ege") },
            },
            CancellationToken.None));

        Assert.Equal("Marmara", updated.CustomValues["bolge"].GetString());
    }

    // ---- The flow sees the values (A4 prerequisite, A5 skip rules) ----

    [Fact]
    public async Task CreateProject_SkipRuleOnACustomField_DecidesTheFirstStep()
    {
        var company = await OpenCompanyAsync();
        await AddAsync(company, Region());
        await using (var scope = database.CreateScope(company.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var first = await db.ProcessSteps.Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
                .OrderBy(s => s.Order).FirstAsync();
            first.Conditions = [new StepConditionRule("custom:bolge", ConditionOperator.Equals, "Ege")];
            await db.SaveChangesAsync();
        }

        var ege = await ProjectsAsync(company, s => s.CreateAsync(Request("""{ "bolge": "Ege" }"""), CancellationToken.None));
        var marmara = await ProjectsAsync(company, s => s.CreateAsync(Request("""{ "bolge": "Marmara" }"""), CancellationToken.None));

        Assert.Equal("1", ege.CurrentStep!.Code);
        Assert.NotEqual("1", marmara.CurrentStep!.Code);
    }

    [Fact]
    public async Task Process_RequestFormComplete_AsksForTheRequiredCustomFields()
    {
        var company = await OpenCompanyAsync();
        var project = await ProjectsAsync(company, s => s.CreateAsync(Request(), CancellationToken.None));
        // The field becomes required after the request was opened: step 3 (Talep Kayıt Formu) now waits for it.
        await AddAsync(company, Region(isRequired: true));
        await using (var scope = database.CreateScope(company.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var row = await db.Projects.SingleAsync(p => p.Id == project.Id);
            row.CurrentStepId = await db.ProcessSteps
                .Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest && s.Code == "3")
                .Select(s => s.Id).SingleAsync();
            await db.SaveChangesAsync();
        }

        await using var processScope = database.CreateScope(company.TenantId);
        var process = new ProcessService(
            processScope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true),
            new RecordingNotificationService(),
            TimeProvider.System,
            processScope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        var state = await process.GetProjectProcessAsync(project.Id, CancellationToken.None);

        var unmet = Assert.Single(state.AvailableActions.UnmetPrerequisites);
        Assert.Equal(PrerequisiteKind.RequestFormComplete, unmet.Kind);
    }

    [Fact]
    public async Task UpdateProject_ValueChange_IsOneAuditField()
    {
        var company = await OpenCompanyAsync();
        await AddAsync(company, Region());
        var project = await ProjectsAsync(company, s =>
            s.CreateAsync(Request("""{ "bolge": "Marmara" }"""), CancellationToken.None));

        await ProjectsAsync(company, s => s.UpdateAsync(project.Id,
            new UpdateProjectDto
            {
                Name = project.Name, Description = project.Description, Source = project.Source,
                RequestChannel = project.RequestChannel, RequestType = project.RequestType,
                CustomerName = project.CustomerName, RequesterName = project.RequesterName,
                RequiresOffer = project.RequiresOffer, RequestDate = project.RequestDate,
                CustomValues = new() { ["bolge"] = JsonSerializer.SerializeToElement("Ege") },
            },
            CancellationToken.None));

        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var changes = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Project) && a.EntityId == project.Id)
            .Select(a => a.Changes)
            .ToListAsync();
        Assert.Contains(changes, c => c.Contains("CustomValues") && c.Contains("Marmara") && c.Contains("Ege"));
    }
}
