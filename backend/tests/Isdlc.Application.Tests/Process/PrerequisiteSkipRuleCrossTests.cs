using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Documents;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Cross tests (PLANNING.md): Dide → Erva's A4 step prerequisites and A5 skip rules, on a real database and through the
/// process commands. They cover what the pure tests (StepPrerequisitesTests, StepConditionsTests) cannot: the facts the
/// service collects for the prerequisites not tried on the database yet (requirements, documents, several at once), the
/// template copy, the company separation, and a custom field rule deciding where a completed step goes.
/// Each test opens its own company and changes only its copy of the flow, so Ithinka and Deneme are never changed.
/// Expectations: docs/configurability.md → Adım ön koşulları, Atlama kuralları.
/// </summary>
[Collection("Database")]
public class PrerequisiteSkipRuleCrossTests(DatabaseFixture database)
{
    private sealed record Company(int TenantId, int AnalystId);

    private async Task<Company> OpenCompanyAsync()
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant { Code = $"onk-{Guid.NewGuid():N}"[..20], Name = "Ön Koşul A.Ş.", CreatedAt = DateTime.UtcNow };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        var analyst = await TestUsers.CreateAsync(database, null, permission: Permission.Analysis, tenantId: tenantId);
        await TestUsers.CreateAsync(database, null, permission: Permission.ProjectManagement, tenantId: tenantId);
        await using (var scope = database.CreateScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
            await transaction.CommitAsync();
        }

        return new Company(tenantId, analyst.Id);
    }

    /// <summary>Changes the company's copy of a step of the new software project flow, as A7's editing will.</summary>
    private async Task SetStepAsync(
        Company company, string code,
        List<StepPrerequisite>? prerequisites = null, List<StepConditionRule>? conditions = null)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var step = await db.ProcessSteps.SingleAsync(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest && s.Code == code);
        if (prerequisites is not null)
        {
            step.Prerequisites = prerequisites;
        }
        if (conditions is not null)
        {
            step.Conditions = conditions;
        }
        await db.SaveChangesAsync();
    }

    private async Task<T> ProcessAsync<T>(Company company, Func<IProcessService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var service = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AnalystId, company.TenantId),
            new RecordingNotificationService(),
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await action(service);
    }

    private async Task<int> StepIdAsync(Company company, string code) =>
        await TestProjects.StepIdAsync(database, code, ProcessFlowCodes.NewRequest, company.TenantId);

    private async Task<ProjectProcessDto> CompleteAsync(Company company, int projectId, string code)
    {
        var stepId = await StepIdAsync(company, code);
        return await ProcessAsync(company, s =>
            s.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = stepId }, CancellationToken.None));
    }

    private async Task AddAsync(Company company, Func<IsdlcDbContext, Task> add)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        await add(db);
        await db.SaveChangesAsync();
    }

    private Task AddRequirementAsync(Company company, int projectId, string code, RequirementStatus status) =>
        AddAsync(company, db =>
        {
            db.Requirements.Add(new Requirement
            {
                ProjectId = projectId, Code = code, Title = $"Gereksinim {code}", Type = RequirementType.Functional,
                Priority = RequirementPriority.MustHave, Status = status,
            });
            return Task.CompletedTask;
        });

    private async Task<string> CurrentStepCodeAsync(Company company, int projectId)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.Projects.Where(p => p.Id == projectId).Select(p => p.CurrentStep!.Code).SingleAsync();
    }

    // ---- A4: requirements approved ----

    [Fact]
    public async Task RequirementsApproved_NoRequirementYet_IsRefused()
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "6", prerequisites: [new(PrerequisiteKind.RequirementsApproved)]);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(company, projectId, "6"));

        Assert.Equal(
            "Bu adım tamamlanamaz: Gereksinimler onaylanmalı; taslak ya da incelemede gereksinim kalmamalı.", error.Message);
        Assert.Equal("6", await CurrentStepCodeAsync(company, projectId));
    }

    [Theory]
    [InlineData(RequirementStatus.Draft)]
    [InlineData(RequirementStatus.InReview)]
    public async Task RequirementsApproved_OneStillOpen_IsRefused(RequirementStatus open)
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "6", prerequisites: [new(PrerequisiteKind.RequirementsApproved)]);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AddRequirementAsync(company, projectId, "GR-001", RequirementStatus.Approved);
        await AddRequirementAsync(company, projectId, "GR-002", open);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(company, projectId, "6"));
    }

    [Fact]
    public async Task RequirementsApproved_ApprovedAndCancelledOnly_MovesOn()
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "6", prerequisites: [new(PrerequisiteKind.RequirementsApproved)]);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AddRequirementAsync(company, projectId, "GR-001", RequirementStatus.Approved);
        await AddRequirementAsync(company, projectId, "GR-002", RequirementStatus.Cancelled);

        await CompleteAsync(company, projectId, "6");

        Assert.NotEqual("6", await CurrentStepCodeAsync(company, projectId));
    }

    // ---- A4: document approved ----

    private async Task<(string Code, string Name, int Id)> FirstDocumentTypeAsync(Company company)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var type = await db.DocumentTypes.OrderBy(t => t.Id).Select(t => new { t.Code, t.Name, t.Id }).FirstAsync();
        return (type.Code, type.Name, type.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(DocumentStatus.InProgress)]
    [InlineData(DocumentStatus.InReview)]
    public async Task DocumentApproved_NotApproved_IsRefusedNamingTheDocument(DocumentStatus? status)
    {
        var company = await OpenCompanyAsync();
        var document = await FirstDocumentTypeAsync(company);
        await SetStepAsync(company, "6", prerequisites: [new(PrerequisiteKind.DocumentApproved, document.Code)]);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        if (status is { } s)
        {
            await AddAsync(company, db =>
            {
                db.ProjectDocuments.Add(new ProjectDocument { ProjectId = projectId, DocumentTypeId = document.Id, Status = s });
                return Task.CompletedTask;
            });
        }

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(company, projectId, "6"));

        Assert.Equal($"Bu adım tamamlanamaz: «{document.Name}» onaylanmalı.", error.Message);
    }

    [Theory]
    [InlineData(DocumentStatus.Approved)]
    [InlineData(DocumentStatus.NotRequired)]
    public async Task DocumentApproved_ApprovedOrNotRequired_MovesOn(DocumentStatus status)
    {
        var company = await OpenCompanyAsync();
        var document = await FirstDocumentTypeAsync(company);
        await SetStepAsync(company, "6", prerequisites: [new(PrerequisiteKind.DocumentApproved, document.Code)]);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AddAsync(company, db =>
        {
            db.ProjectDocuments.Add(new ProjectDocument { ProjectId = projectId, DocumentTypeId = document.Id, Status = status });
            return Task.CompletedTask;
        });

        await CompleteAsync(company, projectId, "6");

        Assert.NotEqual("6", await CurrentStepCodeAsync(company, projectId));
    }

    // ---- A4: several prerequisites ----

    [Fact]
    public async Task SeveralPrerequisites_ScreenListsEveryMissingOne_AndTheCommandNamesTheFirst()
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "6", prerequisites:
            [new(PrerequisiteKind.LevelApplied), new(PrerequisiteKind.RequirementsApproved), new(PrerequisiteKind.RiskExists)]);
        var projectId = await TestProjects.CreateAsync(database, "6", level: 2, tenantId: company.TenantId);

        var process = await ProcessAsync(company, s => s.GetProjectProcessAsync(projectId, CancellationToken.None));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(company, projectId, "6"));

        // The level is applied, so only the other two are missing, in the step's order.
        Assert.Equal(
            [PrerequisiteKind.RequirementsApproved, PrerequisiteKind.RiskExists],
            process.AvailableActions.UnmetPrerequisites.Select(u => u.Kind));
        Assert.False(process.AvailableActions.CanComplete);
        Assert.StartsWith("Bu adım tamamlanamaz: Gereksinimler onaylanmalı", error.Message);
    }

    [Fact]
    public async Task PrerequisiteAddedWhileTheProjectWaits_OnlyStopsTheCompletion()
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "6", prerequisites: []);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await SetStepAsync(company, "6", prerequisites: [new(PrerequisiteKind.RiskExists)]);

        var process = await ProcessAsync(company, s => s.GetProjectProcessAsync(projectId, CancellationToken.None));

        // The project stays where it is and the screen says what to do; nothing is asked for afterwards.
        Assert.Equal("6", process.CurrentStep!.Code);
        Assert.Equal("Risklere git", Assert.Single(process.AvailableActions.UnmetPrerequisites).LinkText);
    }

    // ---- A4: the template copy and the company separation ----

    [Fact]
    public async Task Template_EveryCompanyCopy_GetsThePrerequisites()
    {
        var company = await OpenCompanyAsync();

        await using var scope = database.CreateScope(company.TenantId);
        var steps = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProcessSteps
            .AsNoTracking()
            .Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
            .Select(s => new { s.Code, s.Prerequisites })
            .ToListAsync();

        // The JSON column is filtered here, not in SQL.
        Assert.Equal(["3", "4", "5", "6", "8"], steps.Where(s => s.Prerequisites.Count > 0).Select(s => s.Code).Order());
    }

    [Fact]
    public async Task OneCompanysPrerequisite_DoesNotReachAnother()
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "7", prerequisites: [new(PrerequisiteKind.RequirementsApproved)]);

        await using var scope = database.CreateScope(database.DenemeId);
        var step7 = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProcessSteps
            .AsNoTracking()
            .SingleAsync(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest && s.Code == "7");

        Assert.Empty(step7.Prerequisites);
    }

    // ---- A5: a skip rule on a custom field decides where a completed step goes ----

    /// <summary>An active company field: a rule on a field the company does not have (or switched off) never holds.</summary>
    private Task AddFieldAsync(Company company, string key, CustomFieldType type, List<string>? options = null) =>
        AddAsync(company, db =>
        {
            db.CustomFields.Add(new CustomField { Key = key, Label = key, Type = type, Options = options ?? [] });
            return Task.CompletedTask;
        });

    private async Task<int> ProjectAtStep6WithRegionAsync(Company company, string region)
    {
        await AddFieldAsync(company, "bolge", CustomFieldType.Select, ["Ege", "Marmara"]);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AddAsync(company, async db =>
        {
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.CustomValues = $$"""{"bolge":"{{region}}"}""";
        });
        return projectId;
    }

    [Theory]
    [InlineData("Ege", "7")]
    [InlineData("Marmara", "8")]
    public async Task Complete_NextStepWithACustomFieldRule_IsDoneOnlyWhenItHolds(string region, string expectedNext)
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "6", prerequisites: []);
        await SetStepAsync(company, "7", conditions: [new("custom:bolge", ConditionOperator.Equals, "Ege")]);
        var projectId = await ProjectAtStep6WithRegionAsync(company, region);

        await CompleteAsync(company, projectId, "6");

        Assert.Equal(expectedNext, await CurrentStepCodeAsync(company, projectId));
    }

    [Fact]
    public async Task GetProcess_StepWhoseCustomFieldRuleFails_IsListedAsSkipped()
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "7", conditions: [new("custom:bolge", ConditionOperator.Equals, "Ege")]);
        var projectId = await ProjectAtStep6WithRegionAsync(company, "Marmara");

        var process = await ProcessAsync(company, s => s.GetProjectProcessAsync(projectId, CancellationToken.None));

        Assert.Contains(await StepIdAsync(company, "7"), process.SkippedStepIds);
    }

    [Fact]
    public async Task Complete_RuleOnAFieldTheRecordHasNoValueFor_SkipsTheStep()
    {
        var company = await OpenCompanyAsync();
        await SetStepAsync(company, "6", prerequisites: []);
        await SetStepAsync(company, "7", conditions: [new("custom:butce-onayli", ConditionOperator.Equals, "true")]);
        await AddFieldAsync(company, "butce-onayli", CustomFieldType.Boolean);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);

        await CompleteAsync(company, projectId, "6");

        // whenEmpty defaults to "notMet": a field nobody filled in does not make the step happen.
        Assert.Equal("8", await CurrentStepCodeAsync(company, projectId));
    }
}
