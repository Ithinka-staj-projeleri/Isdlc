using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
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

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// The company administrator edits the company's copy of a flow (Faz 7 · A7). Every test opens its own company with the
/// templates, an administrator and an analyst, so Ithinka's and Deneme's flows are never changed.
/// Rules: docs/configurability.md → Akış düzenleme.
/// </summary>
[Collection("Database")]
public class ProcessFlowEditTests(DatabaseFixture database)
{
    private const string Flow = ProcessFlowCodes.NewRequest;

    private record Company(int TenantId, int AdminId, int AnalystId);

    private async Task<Company> CreateCompanyAsync()
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant { Code = $"akis-{Guid.NewGuid():N}"[..20], Name = "Akış A.Ş.", CreatedAt = DateTime.UtcNow };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        await using (var scope = database.CreateScope(tenantId))
        {
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
        }

        var admin = await TestUsers.CreateAsync(database, null, permission: null, tenantId: tenantId, isTenantAdmin: true);
        var analyst = await TestUsers.CreateAsync(database, null, permission: Permission.Analysis, tenantId: tenantId);
        await TestUsers.CreateAsync(database, null, permission: Permission.ProjectManagement, tenantId: tenantId);
        return new Company(tenantId, admin.Id, analyst.Id);
    }

    private async Task<T> AsAsync<T>(Company company, Func<IProcessFlowEditService, IProcessService, IApplicationDbContext, Task<T>> action,
        bool admin = true)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new TestUser(admin ? company.AdminId : company.AnalystId, company.TenantId, isTenantAdmin: admin);
        var process = new ProcessService(db, user, new RecordingNotificationService(), TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await action(new ProcessFlowEditService(db, user, process), process, db);
    }

    private Task<ProcessFlowDto> FlowAsync(Company company, string code = Flow) =>
        AsAsync(company, (_, process, _) => process.GetFlowAsync(code, includeInactive: true, default));

    private static IReadOnlyList<ProcessStepDto> Steps(ProcessFlowDto flow) => flow.Stages.SelectMany(s => s.Steps).ToList();

    private static ProcessStepDto Step(ProcessFlowDto flow, string code) => Steps(flow).Single(s => s.Code == code);

    /// <summary>A step request with the step's current values, to change only what a test needs.</summary>
    private static SaveStepDto AsRequest(ProcessStepDto step, string rowVersion) => new()
    {
        RowVersion = rowVersion, Text = step.Text, IsDecision = step.IsDecision, NoBranchText = step.NoBranchText,
        ResponsiblePermission = step.ResponsiblePermission, ApproverPermission = step.ApproverPermission,
        NoOutcome = step.NoOutcome,
        NoJumps = step.NoJumps.Select(j => new StepJumpDto { TargetStepId = j.TargetStepId, Label = j.Label }).ToList(),
        Conditions = step.Conditions.ToList(), Prerequisites = step.Prerequisites.ToList(),
    };

    private async Task<int> ProjectOnStepAsync(Company company, int stepId)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = new Project
        {
            Code = $"T-{Guid.NewGuid():N}"[..14], Name = "Bekleyen talep", Description = "Test", Source = RequestSource.Internal,
            RequestChannel = RequestChannel.Email, RequestType = RequestType.NewProject, CustomerName = "Test",
            RequesterName = "Test", Status = ProjectStatus.Active, RequiresOffer = true, RequestDate = new DateOnly(2026, 11, 1),
            ProcessFlowId = await db.ProcessFlows.Where(f => f.Code == Flow).Select(f => f.Id).SingleAsync(),
            CurrentStepId = stepId, BusinessAnalystId = company.AnalystId,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    // ---- Who ----

    [Fact]
    public async Task Edit_NotTheCompanyAdministrator_IsForbidden()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(company,
            (edit, _, _) => edit.UpdateFlowAsync(Flow, new UpdateFlowDto { RowVersion = flow.RowVersion, Name = "X" }, default),
            admin: false));

        Assert.Equal("Bu işlem için şirket yöneticisi olmalısınız.", error.Message);
    }

    [Fact]
    public async Task GetFlow_IncludeInactive_IsOnlyForTheAdministrator()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        await AsAsync(company, (edit, _, _) => edit.SetStepActiveAsync(
            Step(flow, "2").Id, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default));

        var asAdmin = await FlowAsync(company);
        var asAnalyst = await AsAsync(company, (_, process, _) => process.GetFlowAsync(Flow, includeInactive: true, default), admin: false);

        Assert.Contains(Steps(asAdmin), s => !s.IsActive);
        Assert.All(Steps(asAnalyst), s => Assert.True(s.IsActive));
    }

    [Fact]
    public async Task Edit_AnotherCompanysFlow_IsNotFound()
    {
        // The administrator of a new company reaches only its own copies: Ithinka's step ids are not found.
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var ithinkaStepId = await TestProjects.StepIdAsync(database, "2");

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(company, (edit, _, _) => edit.SetStepActiveAsync(
            ithinkaStepId, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default)));
    }

    // ---- Flow ----

    [Fact]
    public async Task Edit_StaleVersion_IsAConflict()
    {
        var company = await CreateCompanyAsync();
        var before = await FlowAsync(company);
        await AsAsync(company, (edit, _, _) => edit.UpdateFlowAsync(Flow,
            new UpdateFlowDto { RowVersion = before.RowVersion, Name = "Yeni ad", UsedFor = ProcessFlowUse.NewRequest }, default));

        var error = await Assert.ThrowsAsync<ConflictException>(() => AsAsync(company, (edit, _, _) => edit.UpdateFlowAsync(Flow,
            new UpdateFlowDto { RowVersion = before.RowVersion, Name = "Eski sürümle", UsedFor = ProcessFlowUse.NewRequest }, default)));

        Assert.Equal("Akış bu arada değişti; sayfayı yenileyin.", error.Message);
    }

    [Fact]
    public async Task UpdateFlow_UseTakenByAnotherFlow_IsAConflict_ThenMovesWhenFreed()
    {
        var company = await CreateCompanyAsync();
        var copy = await AsAsync(company, (edit, _, _) =>
            edit.CopyFlowAsync(Flow, new CopyFlowDto { Code = "bizim-talep", Name = "Bizim talep akışı" }, default));

        var error = await Assert.ThrowsAsync<ConflictException>(() => AsAsync(company, (edit, _, _) => edit.UpdateFlowAsync(
            copy.Code, new UpdateFlowDto { RowVersion = copy.RowVersion, Name = copy.Name, UsedFor = ProcessFlowUse.NewRequest }, default)));
        Assert.Equal("Bu kullanım başka bir akışta seçili: Yeni Yazılım Projesi.", error.Message);

        var template = await FlowAsync(company);
        await AsAsync(company, (edit, _, _) => edit.UpdateFlowAsync(
            Flow, new UpdateFlowDto { RowVersion = template.RowVersion, Name = template.Name, UsedFor = null }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(company,
            (_, _, db) => db.ProcessFlows.FlowIdForAsync(ProcessFlowUse.NewRequest, default)));

        var moved = await AsAsync(company, (edit, _, _) => edit.UpdateFlowAsync(
            copy.Code, new UpdateFlowDto { RowVersion = copy.RowVersion, Name = copy.Name, UsedFor = ProcessFlowUse.NewRequest }, default));
        Assert.Equal(ProcessFlowUse.NewRequest, moved.UsedFor);
        Assert.Equal(moved.Id, await AsAsync(company, (_, _, db) => db.ProcessFlows.FlowIdForAsync(ProcessFlowUse.NewRequest, default)));
    }

    [Fact]
    public async Task UpdateFlow_UseOfAnotherKind_IsAFieldError()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);

        var error = await Assert.ThrowsAsync<FieldValidationException>(() => AsAsync(company, (edit, _, _) => edit.UpdateFlowAsync(
            Flow, new UpdateFlowDto { RowVersion = flow.RowVersion, Name = flow.Name, UsedFor = ProcessFlowUse.Billing }, default)));

        Assert.Equal(["Bu akış bu kayıtlar için kullanılamaz."], error.Errors["usedFor"]);
    }

    [Fact]
    public async Task CopyFlow_IsTheCompanysOwn_WithTheSameStepsAndReturns()
    {
        var company = await CreateCompanyAsync();
        var source = await FlowAsync(company);

        var copy = await AsAsync(company, (edit, _, _) =>
            edit.CopyFlowAsync(Flow, new CopyFlowDto { Code = "kopya", Name = "Kopya akış" }, default));

        Assert.True(copy.IsCustomized);
        Assert.Null(copy.UsedFor);
        Assert.Equal(Steps(source).Select(s => (s.Code, s.Text)), Steps(copy).Select(s => (s.Code, s.Text)));
        var copiedReturn = Assert.Single(Step(copy, "10").NoJumps);
        Assert.Equal(Step(copy, "9").Id, copiedReturn.TargetStepId);
        await Assert.ThrowsAsync<ConflictException>(() => AsAsync(company, (edit, _, _) =>
            edit.CopyFlowAsync(Flow, new CopyFlowDto { Code = "kopya", Name = "İkinci" }, default)));
    }

    // ---- Steps ----

    [Fact]
    public async Task AddStep_AfterAStep_IsNumberedInPlaceAndTheRestMoveOn()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var oldSix = Step(flow, "6");

        var edited = await AsAsync(company, (edit, _, _) => edit.AddStepAsync(Flow, new AddStepDto
        {
            RowVersion = flow.RowVersion, StageId = flow.Stages[0].Id, AfterStepId = Step(flow, "5").Id,
            Text = "Gizlilik sözleşmesi imzalanır.", ResponsiblePermission = Permission.Analysis,
            Prerequisites = [new StepPrerequisite(PrerequisiteKind.MeetingExists)],
        }, default));

        Assert.Equal(Steps(flow).Count + 1, Steps(edited).Count);
        Assert.Equal("Gizlilik sözleşmesi imzalanır.", Step(edited, "6").Text);
        Assert.Equal(oldSix.Id, Step(edited, "7").Id);
        Assert.Equal(Enumerable.Range(1, Steps(edited).Count).Select(n => n.ToString()), Steps(edited).Select(s => s.Code));
        Assert.True(edited.IsCustomized);
    }

    [Fact]
    public async Task UpdateStep_InvalidRulesAndReturns_AreFieldErrorsAtTheirPlace()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var request = AsRequest(Step(flow, "6"), flow.RowVersion);
        request.ApproverPermission = Permission.Approval;
        request.Conditions = [new("source", ConditionOperator.Equals, "External"), new("budget", ConditionOperator.Equals, "x")];
        request.Prerequisites = [new(PrerequisiteKind.DocumentApproved, "YOK")];
        request.NoJumps = [new StepJumpDto { TargetStepId = Step(flow, "2").Id, Label = "Geri" }];

        var error = await Assert.ThrowsAsync<FieldValidationException>(() => AsAsync(company,
            (edit, _, _) => edit.UpdateStepAsync(Step(flow, "6").Id, request, default)));

        Assert.Equal(["Onaylayan yalnızca karar adımında seçilir."], error.Errors["approverPermission"]);
        Assert.Equal(["Dönüş yalnızca karar adımında seçilir."], error.Errors["noJumps"]);
        Assert.Equal(["Bilinmeyen alan: «budget»."], error.Errors["conditions[1]"]);
        Assert.Equal(["Şirkette «YOK» kodlu bir doküman türü yok."], error.Errors["prerequisites[0]"]);
        Assert.False(error.Errors.ContainsKey("conditions[0]"));
    }

    [Fact]
    public async Task UpdateStep_ReturnToALaterStep_IsRefused()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var request = AsRequest(Step(flow, "10"), flow.RowVersion);
        request.NoJumps = [new StepJumpDto { TargetStepId = Step(flow, "12").Id, Label = "İleri" }];

        var error = await Assert.ThrowsAsync<FieldValidationException>(() => AsAsync(company,
            (edit, _, _) => edit.UpdateStepAsync(Step(flow, "10").Id, request, default)));

        Assert.Equal(["Yalnızca daha önceki bir adıma dönülebilir."], error.Errors["noJumps[0]"]);
    }

    [Fact]
    public async Task UpdateStep_Saved_StaysAfterTheStartupSync()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var request = AsRequest(Step(flow, "7"), flow.RowVersion);
        request.Text = "Gereksinimler müşteriyle birlikte onaylanır.";
        request.ResponsiblePermission = Permission.ProjectManagement;
        request.Prerequisites = [new(PrerequisiteKind.RequirementsApproved)];
        request.Conditions = [new("level", ConditionOperator.AtLeast, "2", ConditionWhenEmpty.Met)];
        await AsAsync(company, (edit, _, _) => edit.UpdateStepAsync(Step(flow, "7").Id, request, default));

        // The startup sync runs again: an edited flow is the company's own and is left alone.
        await using (var scope = database.CreateScope(company.TenantId))
        {
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
        }

        var step = Step(await FlowAsync(company), "7");
        Assert.Equal("Gereksinimler müşteriyle birlikte onaylanır.", step.Text);
        Assert.Equal(Permission.ProjectManagement, step.ResponsiblePermission);
        Assert.Equal([new StepPrerequisite(PrerequisiteKind.RequirementsApproved)], step.Prerequisites);
        Assert.Equal(["Seviye 2 ve üstü"], step.ConditionLabels);
    }

    [Fact]
    public async Task DeactivateStep_RecordWaitingOnIt_IsAConflict()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        await ProjectOnStepAsync(company, Step(flow, "3").Id);

        var error = await Assert.ThrowsAsync<ConflictException>(() => AsAsync(company, (edit, _, _) => edit.SetStepActiveAsync(
            Step(flow, "3").Id, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default)));

        Assert.StartsWith("Bu adımda bekleyen kayıtlar var: T-", error.Message);
    }

    [Fact]
    public async Task DeactivateStep_AnotherStepReturnsToIt_IsAConflict()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);

        var error = await Assert.ThrowsAsync<ConflictException>(() => AsAsync(company, (edit, _, _) => edit.SetStepActiveAsync(
            Step(flow, "9").Id, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default)));

        Assert.Equal("Bu adıma «Hayır» dönüşü olan adımlar var: 10.", error.Message);
    }

    [Fact]
    public async Task DeactivateStep_IsHiddenAndSkipped_AndTheOthersRenumbered()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var two = Step(flow, "2");
        var three = Step(flow, "3");
        var projectId = await ProjectOnStepAsync(company, Step(flow, "1").Id);

        var edited = await AsAsync(company, (edit, _, _) => edit.SetStepActiveAsync(
            two.Id, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default));

        Assert.Equal($"P{two.Id}", Steps(edited).Single(s => s.Id == two.Id).Code);
        Assert.Equal(three.Id, Step(edited, "2").Id);

        // The record on step 1 goes straight to the old step 3: the inactive step is never reached.
        var process = await AsAsync(company, async (_, process, _) => await process.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = Step(flow, "1").Id }, default), admin: false);
        Assert.Equal(three.Id, process.CurrentStep!.Id);
    }

    [Fact]
    public async Task ActivateStep_ComesBackInItsPlace()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var two = Step(flow, "2");
        var off = await AsAsync(company, (edit, _, _) => edit.SetStepActiveAsync(
            two.Id, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default));

        var on = await AsAsync(company, (edit, _, _) => edit.SetStepActiveAsync(
            two.Id, true, new FlowVersionDto { RowVersion = off.RowVersion }, default));

        Assert.Equal(two.Id, Step(on, "2").Id);
        Assert.Equal(Steps(flow).Select(s => (s.Id, s.Code)), Steps(on).Select(s => (s.Id, s.Code)));
    }

    // ---- Stages and order ----

    [Fact]
    public async Task DeactivateStage_WithActiveSteps_IsAConflict()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);

        var error = await Assert.ThrowsAsync<ConflictException>(() => AsAsync(company, (edit, _, _) => edit.SetStageActiveAsync(
            flow.Stages[3].Id, false, new FlowVersionDto { RowVersion = flow.RowVersion }, default)));

        Assert.Equal("Aşamada aktif adımlar var; önce adımları pasife alın.", error.Message);
    }

    [Fact]
    public async Task AddStage_ComesLast_AndCanTakeAStep()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);

        var withStage = await AsAsync(company, (edit, _, _) => edit.AddStageAsync(
            Flow, new SaveStageDto { RowVersion = flow.RowVersion, Title = "Garanti", ShortTitle = "Garanti" }, default));
        var stage = withStage.Stages[^1];
        var withStep = await AsAsync(company, (edit, _, _) => edit.AddStepAsync(Flow, new AddStepDto
        {
            RowVersion = withStage.RowVersion, StageId = stage.Id, Text = "Garanti süresi takip edilir.",
            ResponsiblePermission = Permission.ProjectManagement,
        }, default));

        Assert.Equal(5, stage.Order);
        Assert.Equal("Garanti süresi takip edilir.", Steps(withStep)[^1].Text);
        Assert.Equal((Steps(flow).Count + 1).ToString(), Steps(withStep)[^1].Code);
    }

    [Fact]
    public async Task Reorder_MovesStepsAndStages_AndTheProjectStageKeepsItsMeaning()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var projectStageId = flow.Stages.Single(s => s.Order == flow.ProjectStageOrder).Id;
        var stages = flow.Stages.Select(s => new StageOrderDto { Id = s.Id, StepIds = s.Steps.Select(x => x.Id).ToList() }).ToList();
        (stages[0].StepIds[0], stages[0].StepIds[1]) = (stages[0].StepIds[1], stages[0].StepIds[0]);
        (stages[2], stages[3]) = (stages[3], stages[2]);

        var edited = await AsAsync(company, (edit, _, _) =>
            edit.ReorderAsync(Flow, new ReorderFlowDto { RowVersion = flow.RowVersion, Stages = stages }, default));

        Assert.Equal(Step(flow, "2").Id, Step(edited, "1").Id);
        Assert.Equal(Step(flow, "1").Id, Step(edited, "2").Id);
        Assert.Equal(edited.Stages.Single(s => s.Id == projectStageId).Order, edited.ProjectStageOrder);
    }

    [Fact]
    public async Task Reorder_MissingAStep_IsRefused()
    {
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var stages = flow.Stages.Select(s => new StageOrderDto { Id = s.Id, StepIds = s.Steps.Select(x => x.Id).ToList() }).ToList();
        stages[0].StepIds.RemoveAt(0);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(company, (edit, _, _) =>
            edit.ReorderAsync(Flow, new ReorderFlowDto { RowVersion = flow.RowVersion, Stages = stages }, default)));

        Assert.Equal("Sıra akışın bütün aşamalarını ve adımlarını bir kez içermelidir.", error.Message);
    }

    [Fact]
    public async Task Reorder_AReturnWouldGoForward_IsRefused()
    {
        // Step 10 returns to 9; putting 10 before 9 would make that a jump forward.
        var company = await CreateCompanyAsync();
        var flow = await FlowAsync(company);
        var stages = flow.Stages.Select(s => new StageOrderDto { Id = s.Id, StepIds = s.Steps.Select(x => x.Id).ToList() }).ToList();
        var nine = Step(flow, "9").Id;
        var ten = Step(flow, "10").Id;
        var withNine = stages.Single(s => s.StepIds.Contains(nine));
        withNine.StepIds.Insert(withNine.StepIds.IndexOf(nine), ten);
        stages.Single(s => s != withNine && s.StepIds.Contains(ten)).StepIds.Remove(ten);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(company, (edit, _, _) =>
            edit.ReorderAsync(Flow, new ReorderFlowDto { RowVersion = flow.RowVersion, Stages = stages }, default)));

        Assert.Equal("Bu sırada şu adımların «Hayır» dönüşü sonraki bir adıma gider: 10.", error.Message);
    }
}
