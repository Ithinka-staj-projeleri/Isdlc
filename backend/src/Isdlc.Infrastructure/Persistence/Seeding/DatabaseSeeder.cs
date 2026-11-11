using Isdlc.Application.Process;
using Isdlc.Domain.Documents;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Isdlc.Infrastructure.Persistence.Seeding.SeedFiles;

namespace Isdlc.Infrastructure.Persistence.Seeding;

/// <summary>
/// Loads the JSON seed files into empty tables. Rules: docs/data-model.md, "Seed verisi".
/// Each part runs only when its table is empty, so restarting the API is safe.
/// </summary>
public class DatabaseSeeder(IsdlcDbContext db, TimeProvider timeProvider, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(bool includeSampleProjects, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await SeedProcessFlowAsync(cancellationToken);
        await SeedDocumentTypesAsync(cancellationToken);
        await SeedUsersAsync(cancellationToken);
        if (includeSampleProjects)
        {
            await SeedSampleProjectsAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// One flow per file in Seed/flows. A flow that is not in the database yet is added with its stages and
    /// steps (no record uses it, so no migration is needed). An existing flow follows its file (SyncFlowAsync).
    /// </summary>
    private async Task SeedProcessFlowAsync(CancellationToken cancellationToken)
    {
        var files = ReadFlows();
        var flows = await db.ProcessFlows.ToListAsync(cancellationToken);

        foreach (var file in files)
        {
            var flow = flows.SingleOrDefault(f => f.Code == file.Code);
            if (flow is null)
            {
                flow = new ProcessFlow { Code = file.Code };
                db.ProcessFlows.Add(flow);
            }

            flow.Name = file.Name;
            flow.Order = file.Order;
            flow.Kind = file.Kind;
            flow.ProjectStageOrder = file.ProjectStageOrder;

            // A migration may have created the flow row before its stages (moving data into the new structure).
            if (flow.Id != 0 && await db.ProcessStages.AnyAsync(s => s.ProcessFlowId == flow.Id, cancellationToken))
            {
                await SyncFlowAsync(flow, file, cancellationToken);
            }
            else
            {
                AddFlowContent(flow, file);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var fileCodes = files.Select(f => f.Code).ToHashSet();
        foreach (var orphan in flows.Where(f => !fileCodes.Contains(f.Code)))
        {
            logger.LogWarning("Flow {Code} is in the database but has no seed file; it is left as it is.", orphan.Code);
        }
    }

    private void AddFlowContent(ProcessFlow flow, ProcessFlowFile file)
    {
        var stagesByOrder = file.Stages.ToDictionary(
            s => s.Order,
            s => new ProcessStage { ProcessFlow = flow, Order = s.Order, Title = s.Title, ShortTitle = s.ShortTitle });

        var stepsByCode = file.Steps.ToDictionary(
            s => s.Code,
            s => new ProcessStep
            {
                ProcessFlow = flow,
                ProcessStage = stagesByOrder.GetValueOrDefault(s.StageOrder)
                    ?? throw new InvalidOperationException(
                        $"Flow {file.Code}, step {s.Code}: stage {s.StageOrder} does not exist."),
                Code = s.Code,
                Order = s.Order,
                Text = s.Text,
                IsDecision = s.IsDecision,
                NoBranchText = s.NoBranchText,
                ResponsibleRole = s.ResponsibleRole,
                ApproverRole = s.ApproverRole,
                NoOutcome = s.NoOutcome,
                RequiresExternalRequest = s.RequiresExternalRequest,
                RequiresOffer = s.RequiresOffer,
                MinLevel = s.MinLevel,
            });

        foreach (var stepSeed in file.Steps)
        {
            foreach (var jump in stepSeed.NoJumps)
            {
                stepsByCode[stepSeed.Code].NoJumps.Add(new ProcessStepJump
                {
                    TargetStep = FindStep(stepsByCode, jump.TargetStepCode, $"flow {file.Code}, jump from {stepSeed.Code}"),
                    Label = jump.Label,
                });
            }
        }

        db.ProcessStages.AddRange(stagesByOrder.Values);
        db.ProcessSteps.AddRange(stepsByCode.Values);

        logger.LogInformation("Seeded flow {Flow}: {Stages} stages and {Steps} steps.",
            file.Code, stagesByOrder.Count, stepsByCode.Count);
    }

    /// <summary>
    /// Brings an existing flow in line with its file, matching stages by order and steps by code:
    /// titles, texts, "No" explanations, roles, outcomes, conditions and return options. Ids stay the same, so
    /// projects and their process history are untouched. Adding, removing or reordering steps is not done here
    /// (it would move projects between steps); such a difference is only logged and needs its own migration.
    /// </summary>
    private async Task SyncFlowAsync(ProcessFlow flow, ProcessFlowFile file, CancellationToken cancellationToken)
    {
        var stages = await db.ProcessStages.Where(s => s.ProcessFlowId == flow.Id).ToListAsync(cancellationToken);
        var steps = await db.ProcessSteps
            .Include(s => s.NoJumps)
            .Where(s => s.ProcessFlowId == flow.Id)
            .ToListAsync(cancellationToken);
        var stepsByCode = steps.ToDictionary(s => s.Code);

        var fileCodes = file.Steps.Select(s => s.Code).ToHashSet();
        if (stages.Count != file.Stages.Count || !fileCodes.SetEquals(stepsByCode.Keys)
            || file.Steps.Any(s => stepsByCode[s.Code].Order != s.Order))
        {
            logger.LogWarning(
                "Flow {Flow}: the seed file adds, removes or reorders stages or steps; that needs a migration. " +
                "Only texts, roles, conditions and return options of matching steps are updated.", file.Code);
        }

        var changed = 0;
        foreach (var stageSeed in file.Stages)
        {
            if (stages.FirstOrDefault(s => s.Order == stageSeed.Order) is not { } stage)
            {
                continue;
            }

            changed += Set(stage.Title, stageSeed.Title, v => stage.Title = v);
            changed += Set(stage.ShortTitle, stageSeed.ShortTitle, v => stage.ShortTitle = v);
        }

        foreach (var stepSeed in file.Steps)
        {
            if (!stepsByCode.TryGetValue(stepSeed.Code, out var step))
            {
                continue;
            }

            changed += Set(step.Text, stepSeed.Text, v => step.Text = v);
            changed += Set(step.NoBranchText, stepSeed.NoBranchText, v => step.NoBranchText = v);
            changed += Set(step.IsDecision, stepSeed.IsDecision, v => step.IsDecision = v);
            changed += Set(step.ResponsibleRole, stepSeed.ResponsibleRole, v => step.ResponsibleRole = v);
            changed += Set(step.ApproverRole, stepSeed.ApproverRole, v => step.ApproverRole = v);
            changed += Set(step.NoOutcome, stepSeed.NoOutcome, v => step.NoOutcome = v);
            changed += Set(step.RequiresExternalRequest, stepSeed.RequiresExternalRequest, v => step.RequiresExternalRequest = v);
            changed += Set(step.RequiresOffer, stepSeed.RequiresOffer, v => step.RequiresOffer = v);
            changed += Set(step.MinLevel, stepSeed.MinLevel, v => step.MinLevel = v);
            changed += SyncJumps(step, stepSeed, stepsByCode);
        }

        if (changed > 0)
        {
            logger.LogInformation("Flow {Flow}: updated {Changes} values from its seed file.", file.Code, changed);
        }
    }

    /// <summary>Return options follow the file: missing ones are added, extra ones removed, labels updated.</summary>
    private int SyncJumps(ProcessStep step, StepSeed stepSeed, Dictionary<string, ProcessStep> stepsByCode)
    {
        var changed = 0;
        var wanted = stepSeed.NoJumps
            .Where(j => stepsByCode.ContainsKey(j.TargetStepCode))
            .ToDictionary(j => stepsByCode[j.TargetStepCode].Id, j => j.Label);

        foreach (var jump in step.NoJumps.ToList())
        {
            if (!wanted.TryGetValue(jump.TargetStepId, out var label))
            {
                // A return option is reference data; no record points to it (a "No" answer stores the target step).
                db.ProcessStepJumps.Remove(jump);
                changed++;
            }
            else
            {
                changed += Set(jump.Label, label, v => jump.Label = v);
            }
        }

        foreach (var (targetId, label) in wanted)
        {
            if (step.NoJumps.All(j => j.TargetStepId != targetId))
            {
                step.NoJumps.Add(new ProcessStepJump { TargetStepId = targetId, Label = label });
                changed++;
            }
        }

        return changed;
    }

    /// <summary>Sets a value only when it differs, so an unchanged database is not written to. Returns 1 if changed.</summary>
    private static int Set<T>(T current, T wanted, Action<T> set)
    {
        if (EqualityComparer<T>.Default.Equals(current, wanted))
        {
            return 0;
        }

        set(wanted);
        return 1;
    }

    /// <summary>The documents tracked on every project. Linked to stages by order, like the process flow.</summary>
    private async Task SeedDocumentTypesAsync(CancellationToken cancellationToken)
    {
        if (await db.DocumentTypes.AnyAsync(cancellationToken))
        {
            return;
        }

        var file = Read<DocumentTypesFile>("document-types.json");
        // Document types belong to the stages of the request flow (redone with the company's document list in D4).
        var stageIds = await db.ProcessStages
            .Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
            .ToDictionaryAsync(s => s.Order, s => s.Id, cancellationToken);

        db.DocumentTypes.AddRange(file.DocumentTypes.Select(d => new DocumentType
        {
            Code = d.Code,
            Name = d.Name,
            Description = d.Description,
            ProcessStageId = stageIds.TryGetValue(d.StageOrder, out var stageId)
                ? stageId
                : throw new InvalidOperationException($"Document type {d.Code}: stage {d.StageOrder} does not exist."),
            Order = d.Order,
        }));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {DocumentTypes} document types.", file.DocumentTypes.Count);
    }

    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var file = Read<UsersFile>("users.json");
        db.Users.AddRange(file.Users.Select(u => new User
        {
            FullName = u.FullName,
            Email = u.Email,
            Role = u.Role,
        }));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Users} users.", file.Users.Count);
    }

    private async Task SeedSampleProjectsAsync(CancellationToken cancellationToken)
    {
        if (await db.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

        var file = Read<SampleProjectsFile>("sample-projects.json");
        var flow = await db.ProcessFlows.SingleAsync(f => f.Code == ProcessFlowCodes.NewRequest, cancellationToken);
        var steps = await db.ProcessSteps
            .Where(s => s.ProcessFlowId == flow.Id)
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken);
        var stepsByCode = steps.ToDictionary(s => s.Code);
        var users = await db.Users.OrderBy(u => u.Id).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var seed in file.Projects)
        {
            var businessAnalyst = FindUser(users, seed.BusinessAnalystEmail, seed.Code);
            var projectManager = FindUser(users, seed.ProjectManagerEmail, seed.Code);
            var createdById = businessAnalyst?.Id ?? FirstUserWithRole(users, UserRole.BusinessAnalyst).Id;

            var isActive = seed.Status == ProjectStatus.Active;
            var stepCode = (isActive ? seed.CurrentStepCode : seed.ClosedAtStepCode)
                ?? throw new InvalidOperationException(
                    $"Sample project {seed.Code}: {(isActive ? "currentStepCode" : "closedAtStepCode")} is missing.");
            var markerStep = FindStep(stepsByCode, stepCode, $"sample project {seed.Code}");

            var project = new Project
            {
                Code = seed.Code,
                Name = seed.Name,
                Description = seed.Description,
                Source = seed.Source,
                RequestChannel = seed.RequestChannel,
                RequestType = seed.RequestType,
                CustomerName = seed.CustomerName,
                RequesterName = seed.RequesterName,
                RequiresNda = seed.RequiresNda,
                BusinessAnalystId = businessAnalyst?.Id,
                ProjectManagerId = projectManager?.Id,
                Level = seed.Level,
                Priority = seed.Priority,
                Status = seed.Status,
                ProcessFlowId = flow.Id,
                RequiresOffer = seed.RequiresOffer,
                CurrentStepId = isActive ? markerStep.Id : null,
                StartDate = seed.StartDate,
                DueDate = seed.DueDate,
                CreatedAt = ToUtc(seed.StartDate),
                CreatedById = createdById,
            };

            // History: one record per step before the marker step; closed projects also get the closing step.
            // Steps whose conditions do not hold for the project are skipped, as the flow itself does.
            var context = new ConditionContext(seed.Source, seed.RequiresOffer, seed.Level);
            var history = steps
                .Where(s => s.Order < markerStep.Order)
                .Where(s => StepConditions.Applies(
                    new StepCondition(s.RequiresExternalRequest, s.RequiresOffer, s.MinLevel), context))
                .Select(s => (Step: s, Action: s.IsDecision ? StepAction.Yes : StepAction.Completed))
                .ToList();
            if (!isActive)
            {
                history.Add((markerStep, seed.Status == ProjectStatus.Closed ? StepAction.Completed : StepAction.No));
            }

            for (var i = 0; i < history.Count; i++)
            {
                var (step, action) = history[i];
                // One day per step from the start date, never in the future, always increasing.
                var performedAt = Min(ToUtc(seed.StartDate).AddDays(i), now.AddMinutes(i - history.Count));
                var performedById = FirstUserWithRole(users, step.ApproverRole ?? step.ResponsibleRole).Id;

                project.StepRecords.Add(new StepRecord
                {
                    ProcessStepId = step.Id,
                    Action = action,
                    PerformedById = performedById,
                    PerformedAt = performedAt,
                    CreatedAt = performedAt,
                    CreatedById = performedById,
                });
            }

            db.Projects.Add(project);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Projects} sample projects.", file.Projects.Count);
    }

    private static ProcessStep FindStep(IReadOnlyDictionary<string, ProcessStep> stepsByCode, string code, string usedBy) =>
        stepsByCode.GetValueOrDefault(code)
            ?? throw new InvalidOperationException($"Seed error ({usedBy}): step '{code}' does not exist.");

    private static User? FindUser(List<User> users, string? email, string projectCode) =>
        email is null
            ? null
            : users.SingleOrDefault(u => u.Email == email)
                ?? throw new InvalidOperationException($"Sample project {projectCode}: user '{email}' does not exist.");

    private static User FirstUserWithRole(List<User> users, UserRole role) =>
        users.FirstOrDefault(u => u.Role == role)
            ?? throw new InvalidOperationException($"Seed error: no user with role {role}.");

    private static DateTime ToUtc(DateOnly date) => date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
