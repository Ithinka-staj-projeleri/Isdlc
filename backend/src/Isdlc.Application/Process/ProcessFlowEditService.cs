using System.Globalization;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Tenants;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Process;

public class ProcessFlowEditService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    IProcessService processService) : IProcessFlowEditService
{
    /// <summary>The "No" outcomes a step can close a record with; the others are not decisions about a request.</summary>
    private static readonly ProjectStatus[] ClosingOutcomes = [ProjectStatus.Rejected, ProjectStatus.Lost, ProjectStatus.Redirected];

    // ---- Flow ----

    public async Task<ProcessFlowDto> UpdateFlowAsync(string code, UpdateFlowDto request, CancellationToken cancellationToken)
    {
        var flow = await LoadFlowAsync(code, request.RowVersion, cancellationToken);

        if (request.UsedFor is { } use && use != flow.UsedFor)
        {
            if (!Fits(use, flow.Kind))
            {
                throw new FieldValidationException(Error("usedFor", "Bu akış bu kayıtlar için kullanılamaz."));
            }

            var taken = await dbContext.ProcessFlows
                .Where(f => f.UsedFor == use && f.Id != flow.Id)
                .Select(f => f.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (taken is not null)
            {
                throw new ConflictException($"Bu kullanım başka bir akışta seçili: {taken}.");
            }
        }

        flow.Name = request.Name.Trim();
        flow.UsedFor = request.UsedFor;
        return await SaveAsync(flow, renumber: false, cancellationToken);
    }

    public async Task<ProcessFlowDto> CopyFlowAsync(string code, CopyFlowDto request, CancellationToken cancellationToken)
    {
        EnsureTenantAdmin();
        var source = await dbContext.ProcessFlows
            .AsNoTracking()
            .Include(f => f.Stages).ThenInclude(s => s.Steps).ThenInclude(s => s.NoJumps)
            .SingleOrDefaultAsync(f => f.Code == code, cancellationToken)
            ?? throw new NotFoundException("Akış bulunamadı.");

        if (await dbContext.ProcessFlows.AnyAsync(f => f.Code == request.Code, cancellationToken))
        {
            throw new ConflictException("Bu kodla bir akış zaten var.");
        }

        // The copy is the company's own flow: no template (the startup sync never touches it), starts no records yet.
        var copy = new ProcessFlow
        {
            Code = request.Code,
            Name = request.Name.Trim(),
            Kind = source.Kind,
            ProjectStageOrder = source.ProjectStageOrder,
            IsCustomized = true,
            Order = await dbContext.ProcessFlows.MaxAsync(f => (int?)f.Order, cancellationToken) + 1 ?? 1,
        };

        var stepsBySourceId = new Dictionary<int, ProcessStep>();
        foreach (var sourceStage in source.Stages.OrderBy(s => s.Order))
        {
            var stage = new ProcessStage
            {
                ProcessFlow = copy, Order = sourceStage.Order, Title = sourceStage.Title,
                ShortTitle = sourceStage.ShortTitle, IsActive = sourceStage.IsActive,
            };
            copy.Stages.Add(stage);
            foreach (var sourceStep in sourceStage.Steps.OrderBy(s => s.Order))
            {
                var step = new ProcessStep
                {
                    ProcessFlow = copy, ProcessStage = stage, Code = sourceStep.Code, Order = sourceStep.Order,
                    IsActive = sourceStep.IsActive, Text = sourceStep.Text, IsDecision = sourceStep.IsDecision,
                    NoBranchText = sourceStep.NoBranchText, ResponsiblePermission = sourceStep.ResponsiblePermission,
                    ApproverPermission = sourceStep.ApproverPermission, NoOutcome = sourceStep.NoOutcome,
                    Conditions = sourceStep.Conditions.ToList(), Prerequisites = sourceStep.Prerequisites.ToList(),
                };
                stage.Steps.Add(step);
                stepsBySourceId[sourceStep.Id] = step;
            }
        }

        foreach (var sourceStep in source.Stages.SelectMany(s => s.Steps))
        {
            foreach (var jump in sourceStep.NoJumps)
            {
                stepsBySourceId[sourceStep.Id].NoJumps.Add(new ProcessStepJump
                {
                    TargetStep = stepsBySourceId[jump.TargetStepId], Label = jump.Label,
                });
            }
        }

        dbContext.ProcessFlows.Add(copy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await processService.GetFlowAsync(copy.Code, includeInactive: true, cancellationToken);
    }

    // ---- Stages ----

    public async Task<ProcessFlowDto> AddStageAsync(string code, SaveStageDto request, CancellationToken cancellationToken)
    {
        var flow = await LoadFlowAsync(code, request.RowVersion, cancellationToken);
        flow.Stages.Add(new ProcessStage
        {
            ProcessFlow = flow,
            Order = flow.Stages.Max(s => (int?)s.Order) + 1 ?? 1,
            Title = request.Title.Trim(),
            ShortTitle = request.ShortTitle.Trim(),
        });
        return await SaveAsync(flow, renumber: false, cancellationToken);
    }

    public async Task<ProcessFlowDto> UpdateStageAsync(int stageId, SaveStageDto request, CancellationToken cancellationToken)
    {
        var (flow, stage) = await LoadStageAsync(stageId, request.RowVersion, cancellationToken);
        stage.Title = request.Title.Trim();
        stage.ShortTitle = request.ShortTitle.Trim();
        return await SaveAsync(flow, renumber: false, cancellationToken);
    }

    public async Task<ProcessFlowDto> SetStageActiveAsync(
        int stageId, bool active, FlowVersionDto request, CancellationToken cancellationToken)
    {
        var (flow, stage) = await LoadStageAsync(stageId, request.RowVersion, cancellationToken);
        if (!active && stage.Steps.Any(s => s.IsActive))
        {
            throw new ConflictException("Aşamada aktif adımlar var; önce adımları pasife alın.");
        }

        stage.IsActive = active;
        return await SaveAsync(flow, renumber: false, cancellationToken);
    }

    // ---- Steps ----

    public async Task<ProcessFlowDto> AddStepAsync(string code, AddStepDto request, CancellationToken cancellationToken)
    {
        var flow = await LoadFlowAsync(code, request.RowVersion, cancellationToken);
        var stage = flow.Stages.SingleOrDefault(s => s.Id == request.StageId)
            ?? throw new FieldValidationException(Error("stageId", "Aşama bu akışta bulunamadı."));
        if (!stage.IsActive)
        {
            throw new FieldValidationException(Error("stageId", "Pasif bir aşamaya adım eklenemez."));
        }

        var steps = OrderedSteps(flow);
        int position;
        if (request.AfterStepId is { } afterId)
        {
            var after = stage.Steps.SingleOrDefault(s => s.Id == afterId)
                ?? throw new FieldValidationException(Error("afterStepId", "Adım bu aşamada bulunamadı."));
            position = steps.IndexOf(after) + 1;
        }
        else
        {
            // First in the stage: right after the last step of the stages before it.
            position = steps.Count(s => s.ProcessStage.Order < stage.Order);
        }

        var step = new ProcessStep { ProcessFlow = flow, ProcessStage = stage, Code = "~new", Order = int.MaxValue };
        await ApplyStepAsync(flow, step, request, cancellationToken);
        stage.Steps.Add(step);
        steps.Insert(position, step);
        EnsureJumpsGoBack(steps);
        return await SaveAsync(flow, renumber: true, cancellationToken, steps);
    }

    public async Task<ProcessFlowDto> UpdateStepAsync(int stepId, SaveStepDto request, CancellationToken cancellationToken)
    {
        var (flow, step) = await LoadStepAsync(stepId, request.RowVersion, cancellationToken);
        await ApplyStepAsync(flow, step, request, cancellationToken);
        return await SaveAsync(flow, renumber: false, cancellationToken);
    }

    public async Task<ProcessFlowDto> SetStepActiveAsync(
        int stepId, bool active, FlowVersionDto request, CancellationToken cancellationToken)
    {
        var (flow, step) = await LoadStepAsync(stepId, request.RowVersion, cancellationToken);
        if (active == step.IsActive)
        {
            return await processService.GetFlowAsync(flow.Code, includeInactive: true, cancellationToken);
        }

        if (active)
        {
            if (!step.ProcessStage.IsActive)
            {
                throw new ConflictException("Önce adımın aşamasını etkinleştirin.");
            }
        }
        else
        {
            await EnsureCanDeactivateAsync(flow, step, cancellationToken);
        }

        step.IsActive = active;
        return await SaveAsync(flow, renumber: true, cancellationToken);
    }

    public async Task<ProcessFlowDto> ReorderAsync(string code, ReorderFlowDto request, CancellationToken cancellationToken)
    {
        var flow = await LoadFlowAsync(code, request.RowVersion, cancellationToken);
        var stagesById = flow.Stages.ToDictionary(s => s.Id);
        var stepsById = flow.Stages.SelectMany(s => s.Steps).ToDictionary(s => s.Id);

        var stageIds = request.Stages.Select(s => s.Id ?? 0).ToList();
        var stepIds = request.Stages.SelectMany(s => s.StepIds).ToList();
        if (stageIds.Count != stagesById.Count || stageIds.Distinct().Count() != stageIds.Count
            || !stageIds.All(stagesById.ContainsKey)
            || stepIds.Count != stepsById.Count || stepIds.Distinct().Count() != stepIds.Count
            || !stepIds.All(stepsById.ContainsKey))
        {
            throw new BusinessRuleException("Sıra akışın bütün aşamalarını ve adımlarını bir kez içermelidir.");
        }

        // The stage that makes a record a project keeps that meaning wherever it moves.
        var projectStage = flow.Stages.SingleOrDefault(s => s.Order == flow.ProjectStageOrder);

        var ordered = new List<ProcessStep>();
        for (var i = 0; i < request.Stages.Count; i++)
        {
            var stage = stagesById[request.Stages[i].Id!.Value];
            stage.Order = i + 1;
            foreach (var stepId in request.Stages[i].StepIds)
            {
                var step = stepsById[stepId];
                if (step.IsActive && !stage.IsActive)
                {
                    throw new BusinessRuleException("Aktif bir adım pasif bir aşamaya taşınamaz.");
                }

                step.ProcessStage = stage;
                step.ProcessStageId = stage.Id;
                ordered.Add(step);
            }
        }

        if (projectStage is not null)
        {
            flow.ProjectStageOrder = projectStage.Order;
        }

        EnsureJumpsGoBack(ordered);
        return await SaveAsync(flow, renumber: true, cancellationToken, ordered);
    }

    // ---- Rules ----

    /// <summary>Copies the request onto the step after checking it as a whole; 400 with every field error at once.</summary>
    private async Task ApplyStepAsync(ProcessFlow flow, ProcessStep step, SaveStepDto request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors[key] = [message];

        if (!request.IsDecision)
        {
            if (request.ApproverPermission is not null) Add("approverPermission", "Onaylayan yalnızca karar adımında seçilir.");
            if (request.NoOutcome is not null) Add("noOutcome", "Kapanış yalnızca karar adımında seçilir.");
            if (request.NoJumps.Count > 0) Add("noJumps", "Dönüş yalnızca karar adımında seçilir.");
        }
        else if (request.NoOutcome is not null && request.NoJumps.Count > 0)
        {
            Add("noOutcome", "«Hayır» ya kaydı kapatır ya da bir adıma döner; ikisi birden seçilemez.");
        }

        if (request.NoOutcome is { } outcome && !ClosingOutcomes.Contains(outcome))
        {
            Add("noOutcome", "Kapanış sonucu Reddedildi, Kaybedildi ya da Yönlendirildi olmalıdır.");
        }

        // A return goes to an earlier active step of the same flow; the order is checked against the flow as it is now.
        var steps = OrderedSteps(flow);
        var position = step.Id == 0 ? int.MaxValue : steps.FindIndex(s => s.Id == step.Id);
        for (var i = 0; i < request.NoJumps.Count; i++)
        {
            var target = steps.FindIndex(s => s.Id == request.NoJumps[i].TargetStepId);
            if (target < 0 || !steps[target].IsActive)
            {
                Add($"noJumps[{i}]", "Dönülecek adım bu akışın aktif bir adımı olmalıdır.");
            }
            else if (step.Id != 0 && target >= position)
            {
                Add($"noJumps[{i}]", "Yalnızca daha önceki bir adıma dönülebilir.");
            }
        }

        if (request.NoJumps.Select(j => j.TargetStepId).Distinct().Count() != request.NoJumps.Count)
        {
            Add("noJumps", "Aynı adıma iki dönüş eklenemez.");
        }

        foreach (var error in StepRuleValidation.ValidateConditions(request.Conditions, await ConditionFieldsAsync(cancellationToken)))
        {
            Add($"conditions[{error.Index}]", error.Message);
        }

        var forms = (await dbContext.QuestionSets.Select(q => q.Code).ToListAsync(cancellationToken)).ToHashSet();
        var documents = (await dbContext.DocumentTypes.Select(d => d.Code).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var error in StepRuleValidation.ValidatePrerequisites(request.Prerequisites, forms, documents))
        {
            Add($"prerequisites[{error.Index}]", error.Message);
        }

        if (errors.Count > 0)
        {
            throw new FieldValidationException(errors);
        }

        step.Text = request.Text.Trim();
        step.IsDecision = request.IsDecision;
        step.NoBranchText = string.IsNullOrWhiteSpace(request.NoBranchText) ? null : request.NoBranchText.Trim();
        step.ResponsiblePermission = request.ResponsiblePermission!.Value;
        step.ApproverPermission = request.ApproverPermission;
        step.NoOutcome = request.NoOutcome;
        step.Conditions = request.Conditions.ToList();
        step.Prerequisites = request.Prerequisites.ToList();

        // Return options are settings, not records: the old ones are replaced.
        foreach (var jump in step.NoJumps.ToList())
        {
            step.NoJumps.Remove(jump);
            dbContext.ProcessStepJumps.Remove(jump);
        }

        foreach (var jump in request.NoJumps)
        {
            step.NoJumps.Add(new ProcessStepJump { TargetStepId = jump.TargetStepId!.Value, Label = jump.Label.Trim() });
        }
    }

    /// <summary>A step is never deleted; it can be made inactive only when no record waits on it and nothing returns to it.</summary>
    private async Task EnsureCanDeactivateAsync(ProcessFlow flow, ProcessStep step, CancellationToken cancellationToken)
    {
        var waiting = await dbContext.Projects
            .Where(p => p.CurrentStepId == step.Id && (p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Postponed))
            .OrderBy(p => p.Id)
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);
        if (waiting.Count > 0)
        {
            throw new ConflictException($"Bu adımda bekleyen kayıtlar var: {string.Join(", ", waiting)}.");
        }

        var returning = OrderedSteps(flow)
            .Where(s => s.IsActive && s.Id != step.Id && s.NoJumps.Any(j => j.TargetStepId == step.Id))
            .Select(s => s.Code)
            .ToList();
        if (returning.Count > 0)
        {
            throw new ConflictException($"Bu adıma «Hayır» dönüşü olan adımlar var: {string.Join(", ", returning)}.");
        }

        if (OrderedSteps(flow).Count(s => s.IsActive) == 1)
        {
            throw new BusinessRuleException("Akışta en az bir aktif adım olmalıdır.");
        }
    }

    /// <summary>After a reorder every return must still go to an earlier step.</summary>
    private static void EnsureJumpsGoBack(IReadOnlyList<ProcessStep> ordered)
    {
        var position = ordered.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var wrong = ordered
            .Where(s => s.IsActive && s.NoJumps.Any(j => position[j.TargetStepId] >= position[s.Id]))
            .Select(s => s.Code)
            .ToList();
        if (wrong.Count > 0)
        {
            throw new BusinessRuleException(
                $"Bu sırada şu adımların «Hayır» dönüşü sonraki bir adıma gider: {string.Join(", ", wrong)}.");
        }
    }

    private static bool Fits(ProcessFlowUse use, ProcessFlowKind kind) => use switch
    {
        ProcessFlowUse.NewRequest or ProcessFlowUse.AdditionalModule => kind == ProcessFlowKind.Request,
        ProcessFlowUse.Improvement => kind == ProcessFlowKind.Improvement,
        ProcessFlowUse.ResourcePlanning => kind == ProcessFlowKind.ResourcePlanning,
        ProcessFlowUse.Billing => kind == ProcessFlowKind.Billing,
        _ => false,
    };

    /// <summary>The company's active custom fields as rule fields (Faz 7 · A6).</summary>
    private async Task<IReadOnlyList<ConditionField>> ConditionFieldsAsync(CancellationToken cancellationToken)
    {
        var fields = await dbContext.CustomFields
            .AsNoTracking()
            .Where(f => f.IsActive)
            .Select(f => new { f.Key, f.Label, f.Type, f.Options })
            .ToListAsync(cancellationToken);

        return fields
            .Select(f => new ConditionField(StepConditionRule.CustomPrefix + f.Key, f.Label, f.Type switch
            {
                CustomFieldType.Number => ConditionFieldType.Number,
                CustomFieldType.Date => ConditionFieldType.Date,
                CustomFieldType.Select => ConditionFieldType.Choice,
                CustomFieldType.Boolean => ConditionFieldType.Boolean,
                _ => ConditionFieldType.Text,
            }, f.Type == CustomFieldType.Select ? f.Options : null))
            .ToList();
    }

    // ---- Loading and saving ----

    private void EnsureTenantAdmin()
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        if (!currentUser.IsTenantAdmin || currentUser.TenantId is null)
        {
            throw new ForbiddenException(TenantSettingsService.TenantAdminRequired);
        }
    }

    /// <summary>The flow with all its stages, steps and returns, tracked, checked against the version the screen showed.</summary>
    private async Task<ProcessFlow> LoadFlowAsync(string code, string rowVersion, CancellationToken cancellationToken)
    {
        EnsureTenantAdmin();
        var flow = await dbContext.ProcessFlows
            .Include(f => f.Stages).ThenInclude(s => s.Steps).ThenInclude(s => s.NoJumps)
            .SingleOrDefaultAsync(f => f.Code == code, cancellationToken)
            ?? throw new NotFoundException("Akış bulunamadı.");
        ExpectVersion(flow, rowVersion);
        return flow;
    }

    private async Task<(ProcessFlow Flow, ProcessStage Stage)> LoadStageAsync(
        int stageId, string rowVersion, CancellationToken cancellationToken)
    {
        EnsureTenantAdmin();
        var code = await dbContext.ProcessStages.Where(s => s.Id == stageId).Select(s => s.ProcessFlow.Code)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Aşama bulunamadı.");
        var flow = await LoadFlowAsync(code, rowVersion, cancellationToken);
        return (flow, flow.Stages.Single(s => s.Id == stageId));
    }

    private async Task<(ProcessFlow Flow, ProcessStep Step)> LoadStepAsync(
        int stepId, string rowVersion, CancellationToken cancellationToken)
    {
        EnsureTenantAdmin();
        var code = await dbContext.ProcessSteps.Where(s => s.Id == stepId).Select(s => s.ProcessFlow.Code)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Adım bulunamadı.");
        var flow = await LoadFlowAsync(code, rowVersion, cancellationToken);
        return (flow, flow.Stages.SelectMany(s => s.Steps).Single(s => s.Id == stepId));
    }

    /// <summary>A stale version (or one that is not a version at all) is the 409 of an edit made meanwhile.</summary>
    private void ExpectVersion(ProcessFlow flow, string rowVersion)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new ConflictException("Akış bu arada değişti; sayfayı yenileyin.");
        }

        if (!expected.SequenceEqual(flow.RowVersion))
        {
            throw new ConflictException("Akış bu arada değişti; sayfayı yenileyin.");
        }

        // The UPDATE also checks it, so two saves racing each other cannot both win.
        dbContext.Entry(flow).Property(f => f.RowVersion).OriginalValue = expected;
    }

    /// <summary>Steps in flow order: stage by stage, then by order within the stage.</summary>
    private static List<ProcessStep> OrderedSteps(ProcessFlow flow) =>
        flow.Stages.OrderBy(s => s.Order).SelectMany(s => s.Steps.OrderBy(step => step.Order)).ToList();

    /// <summary>
    /// Saves the change and marks the flow as the company's own (the startup sync leaves it alone). With renumber, active
    /// steps are numbered 1, 2, 3… in flow order and inactive ones get "P{Id}": the unique (flow, code) and (flow, order)
    /// indexes would refuse a swap made in one save, so every number is first moved out of the way.
    /// </summary>
    private async Task<ProcessFlowDto> SaveAsync(
        ProcessFlow flow, bool renumber, CancellationToken cancellationToken, List<ProcessStep>? order = null)
    {
        flow.IsCustomized = true;
        dbContext.Entry(flow).Property(f => f.IsCustomized).IsModified = true;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (renumber)
        {
            var steps = order ?? OrderedSteps(flow);
            var stages = flow.Stages.OrderBy(s => s.Order).ToList();
            foreach (var step in steps.Where(s => s.Id != 0))
            {
                step.Code = "~" + step.Id.ToString(CultureInfo.InvariantCulture);
                step.Order = -step.Id;
            }

            foreach (var stage in stages.Where(s => s.Id != 0))
            {
                stage.Order = -stage.Id;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            for (var i = 0; i < stages.Count; i++)
            {
                stages[i].Order = i + 1;
            }

            // An inactive step keeps its place in the order (it comes back there when activated); only active steps
            // count for the numbers people see.
            var number = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                steps[i].Order = i + 1;
                steps[i].Code = steps[i].IsActive
                    ? (++number).ToString(CultureInfo.InvariantCulture)
                    : "P" + steps[i].Id.ToString(CultureInfo.InvariantCulture);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await processService.GetFlowAsync(flow.Code, includeInactive: true, cancellationToken);
    }

    private static Dictionary<string, string[]> Error(string key, string message) => new() { [key] = [message] };
}
