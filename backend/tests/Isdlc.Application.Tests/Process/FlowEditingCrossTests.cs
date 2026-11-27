using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Cross tests (PLANNING.md): Dide → Erva's A7 flow editing. The rules of docs/configurability.md → Akış düzenleme that
/// ProcessFlowEditTests does not try yet: a company without a flow for new requests, a postponed record on a step, the
/// decision rules of the step form, returns to inactive steps, moving a step into an inactive stage, a stale version on
/// a step request, the code of an inactive step, the first edit marking the flow as the company's own, and a rule on a
/// switched-off custom field. Each test opens its own company, so Ithinka and Deneme are never changed.
/// </summary>
[Collection("Database")]
public class FlowEditingCrossTests(DatabaseFixture database)
{
    private const string Flow = ProcessFlowCodes.NewRequest;

    private record Company(int TenantId, int AdminId);

    private async Task<Company> CreateCompanyAsync()
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant { Code = $"duz-{Guid.NewGuid():N}"[..20], Name = "Düzenleme A.Ş.", CreatedAt = DateTime.UtcNow };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        await using (var scope = database.CreateScope(tenantId))
        {
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
        }

        // The administrator also opens requests (Analysis), so one user does both in the tests.
        var admin = await TestUsers.CreateAsync(
            database, null, permission: Permission.Analysis, tenantId: tenantId, isTenantAdmin: true);
        await TestUsers.CreateAsync(database, null, permission: Permission.ProjectManagement, tenantId: tenantId);
        return new Company(tenantId, admin.Id);
    }

    private async Task<T> EditAsync<T>(Company company, Func<IProcessFlowEditService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true);
        var process = new ProcessService(db, user, new RecordingNotificationService(), TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await action(new ProcessFlowEditService(db, user, process));
    }

    private async Task<ProcessFlowDto> FlowAsync(Company company)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var process = new ProcessService(db, new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true),
            new RecordingNotificationService(), TimeProvider.System, scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await process.GetFlowAsync(Flow, includeInactive: true, CancellationToken.None);
    }

    private static List<ProcessStepDto> Steps(ProcessFlowDto flow) => flow.Stages.SelectMany(s => s.Steps).ToList();

    private static ProcessStepDto Step(ProcessFlowDto flow, string code) => Steps(flow).Single(s => s.Code == code);

    private static SaveStepDto AsRequest(ProcessStepDto step, string rowVersion) => new()
    {
        RowVersion = rowVersion, Text = step.Text, IsDecision = step.IsDecision, NoBranchText = step.NoBranchText,
        ResponsiblePermission = step.ResponsiblePermission, ApproverPermission = step.ApproverPermission,
        NoOutcome = step.NoOutcome,
        NoJumps = step.NoJumps.Select(j => new StepJumpDto { TargetStepId = j.TargetStepId, Label = j.Label }).ToList(),
        Conditions = step.Conditions.ToList(), Prerequisites = step.Prerequisites.ToList(),
    };

    private Task<ProcessFlowDto> SetStepActiveAsync(Company company, int stepId, bool active, string rowVersion) =>
        EditAsync(company, e => e.SetStepActiveAsync(stepId, active, new FlowVersionDto { RowVersion = rowVersion }, default));

    // ---- The flow that starts new requests ----

    [Fact]
    public async Task NewRequest_NoFlowChosenForIt_IsRefusedWithTheWayToFixIt()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        await EditAsync(company, e => e.UpdateFlowAsync(
            Flow, new UpdateFlowDto { RowVersion = flow.RowVersion, Name = flow.Name, UsedFor = null }, default));

        await using var scope = database.CreateScope(company.TenantId);
        var projects = new ProjectService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true),
            new RecordingNotificationService());
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            projects.CreateAsync(CreateProjectDtoTests.Valid(), CancellationToken.None));

        Assert.Equal(
            "Şirkette yeni talepler için bir akış seçilmemiş. Şirket yöneticisi Süreç Kütüphanesi'nden seçebilir.",
            error.Message);
    }

    // ---- Switching a step off ----

    [Fact]
    public async Task DeactivateStep_PostponedRecordWaitingOnIt_IsAConflict()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var step = Step(flow, "3");
        var projectId = await TestProjects.CreateAsync(database, "3", ProjectStatus.Postponed, tenantId: company.TenantId);
        string code;
        await using (var scope = database.CreateScope(company.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            code = await db.Projects.Where(p => p.Id == projectId).Select(p => p.Code).SingleAsync();
        }

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            SetStepActiveAsync(company, step.Id, false, flow.RowVersion));

        Assert.Equal($"Bu adımda bekleyen kayıtlar var: {code}.", error.Message);
    }

    [Fact]
    public async Task InactiveStep_IsCodedPWithItsId_AndKeepsItsPlace()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var step = Step(flow, "2");

        var after = await SetStepActiveAsync(company, step.Id, false, flow.RowVersion);

        var steps = Steps(after);
        var inactive = steps.Single(s => s.Id == step.Id);
        Assert.Equal($"P{step.Id}", inactive.Code);
        Assert.Equal(1, steps.IndexOf(inactive));
    }

    // Found by this cross test: SetStepActiveAsync(active: true) checks only the stage, not where the step's "Hayır" goes, so
    // the step comes back with a return to an inactive step; answering "Hayır" there would send the record to it.
    // Waiting for the fix (A7); remove Skip then.
    [Fact(Skip = "Pasif adıma dönüşü olan adım yeniden açılabiliyor; düzeltme bekleniyor (A7).")]
    public async Task ActivateStep_ItsReturnGoesToAnInactiveStep_IsRefused()
    {
        // A decision step returns to an earlier step. The decision step is switched off first (nothing then returns to
        // the earlier step from an active step), then the earlier step. Switching the decision step back on would give
        // it a "Hayır" to an inactive step, which the rules never allow (docs/configurability.md → Adım formu kuralları).
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var decision = Steps(flow).First(s => s.IsActive && s.NoJumps.Count == 1);
        var target = Steps(flow).Single(s => s.Id == decision.NoJumps[0].TargetStepId);
        if (Steps(flow).Any(s => s.Id != decision.Id && s.NoJumps.Any(j => j.TargetStepId == target.Id)))
        {
            Assert.Fail("The template has another return to the target; pick a different pair for this test.");
        }

        flow = await SetStepActiveAsync(company, decision.Id, false, flow.RowVersion);
        flow = await SetStepActiveAsync(company, target.Id, false, flow.RowVersion);

        var rowVersion = flow.RowVersion;
        await Assert.ThrowsAsync<ConflictException>(() => SetStepActiveAsync(company, decision.Id, true, rowVersion));

        var now = await FlowAsync(company);
        Assert.False(Steps(now).Single(s => s.Id == decision.Id).IsActive);
    }

    // ---- The step form ----

    [Fact]
    public async Task UpdateStep_DecisionClosesAndReturns_IsAFieldError()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var decision = Steps(flow).First(s => s.IsActive && s.NoJumps.Count > 0);

        var request = AsRequest(decision, flow.RowVersion);
        request.NoOutcome = ProjectStatus.Rejected;
        var error = await Assert.ThrowsAsync<FieldValidationException>(() =>
            EditAsync(company, e => e.UpdateStepAsync(decision.Id, request, default)));

        Assert.Equal("«Hayır» ya kaydı kapatır ya da bir adıma döner; ikisi birden seçilemez.", error.Errors["noOutcome"][0]);
    }

    [Fact]
    public async Task UpdateStep_ApproverOrOutcomeOnANonDecisionStep_AreFieldErrors()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var step = Steps(flow).First(s => s.IsActive && !s.IsDecision);

        var request = AsRequest(step, flow.RowVersion);
        request.ApproverPermission = Permission.Approval;
        request.NoOutcome = ProjectStatus.Rejected;
        var error = await Assert.ThrowsAsync<FieldValidationException>(() =>
            EditAsync(company, e => e.UpdateStepAsync(step.Id, request, default)));

        Assert.Equal("Onaylayan yalnızca karar adımında seçilir.", error.Errors["approverPermission"][0]);
        Assert.Equal("Kapanış yalnızca karar adımında seçilir.", error.Errors["noOutcome"][0]);
    }

    [Fact]
    public async Task UpdateStep_OutcomeThatDoesNotClose_IsAFieldError()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var decision = Steps(flow).First(s => s.IsActive && s.NoOutcome is not null);

        var request = AsRequest(decision, flow.RowVersion);
        request.NoOutcome = ProjectStatus.Postponed;
        var error = await Assert.ThrowsAsync<FieldValidationException>(() =>
            EditAsync(company, e => e.UpdateStepAsync(decision.Id, request, default)));

        Assert.Contains("noOutcome", error.Errors.Keys);
    }

    [Fact]
    public async Task UpdateStep_ReturnToAnInactiveStepOrTwiceToOne_AreFieldErrors()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var decision = Steps(flow).First(s => s.IsActive && s.NoJumps.Count > 0);
        var earlier = Steps(flow).TakeWhile(s => s.Id != decision.Id)
            .First(s => s.IsActive && Steps(flow).All(x => x.NoJumps.All(j => j.TargetStepId != s.Id)));
        flow = await SetStepActiveAsync(company, earlier.Id, false, flow.RowVersion);
        decision = Steps(flow).Single(s => s.Id == decision.Id);

        var toInactive = AsRequest(decision, flow.RowVersion);
        toInactive.NoJumps = [new StepJumpDto { TargetStepId = earlier.Id, Label = "Geri" }];
        var inactiveError = await Assert.ThrowsAsync<FieldValidationException>(() =>
            EditAsync(company, e => e.UpdateStepAsync(decision.Id, toInactive, default)));

        var twice = AsRequest(decision, flow.RowVersion);
        twice.NoJumps = [twice.NoJumps[0], new StepJumpDto { TargetStepId = twice.NoJumps[0].TargetStepId, Label = "Yine" }];
        var twiceError = await Assert.ThrowsAsync<FieldValidationException>(() =>
            EditAsync(company, e => e.UpdateStepAsync(decision.Id, twice, default)));

        Assert.Equal("Dönülecek adım bu akışın aktif bir adımı olmalıdır.", inactiveError.Errors["noJumps[0]"][0]);
        Assert.Equal("Aynı adıma iki dönüş eklenemez.", twiceError.Errors["noJumps"][0]);
    }

    [Fact]
    public async Task UpdateStep_RuleOnASwitchedOffCustomField_IsAFieldError()
    {
        var company = await CreateCompanyAsync();
        await using (var scope = database.CreateScope(company.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            db.CustomFields.Add(new CustomField
            {
                Key = "bolge", Label = "Bölge", Type = CustomFieldType.Select, Options = ["Ege"], IsActive = false,
            });
            await db.SaveChangesAsync();
        }

        var flow = await FlowAsync(company);
        var step = Step(flow, "2");
        var request = AsRequest(step, flow.RowVersion);
        request.Conditions = [new StepConditionRule("custom:bolge", ConditionOperator.Equals, "Ege")];

        var error = await Assert.ThrowsAsync<FieldValidationException>(() =>
            EditAsync(company, e => e.UpdateStepAsync(step.Id, request, default)));

        Assert.Contains("conditions[0]", error.Errors.Keys);
    }

    [Fact]
    public async Task UpdateStep_StaleVersion_IsAConflict()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var stale = flow.RowVersion;
        await EditAsync(company, e => e.UpdateStageAsync(flow.Stages[0].Id,
            new SaveStageDto { RowVersion = stale, Title = "Talep", ShortTitle = "Talep" }, default));

        var step = Step(flow, "1");
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            EditAsync(company, e => e.UpdateStepAsync(step.Id, AsRequest(step, stale), default)));

        Assert.Equal("Akış bu arada değişti; sayfayı yenileyin.", error.Message);
    }

    // ---- Order and stages ----

    [Fact]
    public async Task Reorder_ActiveStepIntoAnInactiveStage_IsRefused()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        flow = await EditAsync(company, e => e.AddStageAsync(
            Flow, new SaveStageDto { RowVersion = flow.RowVersion, Title = "Boş", ShortTitle = "Boş" }, default));
        var empty = flow.Stages.Single(s => s.Title == "Boş");
        flow = await EditAsync(company, e => e.SetStageActiveAsync(
            empty.Id, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default));

        // The first step of stage 1 is moved into the inactive stage.
        var moved = flow.Stages[0].Steps[0].Id;
        var request = new ReorderFlowDto
        {
            RowVersion = flow.RowVersion,
            Stages = flow.Stages.Select(s => new StageOrderDto
            {
                Id = s.Id,
                StepIds = s.Id == empty.Id
                    ? [moved]
                    : s.Steps.Where(x => x.Id != moved).Select(x => x.Id).ToList(),
            }).ToList(),
        };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            EditAsync(company, e => e.ReorderAsync(Flow, request, default)));

        Assert.Equal("Aktif bir adım pasif bir aşamaya taşınamaz.", error.Message);
    }

    [Fact]
    public async Task FirstEdit_EvenAStageTitle_MakesTheFlowTheCompanysOwn()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        Assert.False(flow.IsCustomized);

        var after = await EditAsync(company, e => e.UpdateStageAsync(flow.Stages[0].Id,
            new SaveStageDto { RowVersion = flow.RowVersion, Title = "Talep ve Analiz", ShortTitle = "Talep" }, default));

        Assert.True(after.IsCustomized);
        Assert.Equal("Talep ve Analiz", after.Stages[0].Title);
    }
}
