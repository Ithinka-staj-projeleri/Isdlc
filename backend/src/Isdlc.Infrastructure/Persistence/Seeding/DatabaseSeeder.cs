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
        await SeedUsersAsync(cancellationToken);
        if (includeSampleProjects)
        {
            await SeedSampleProjectsAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SeedProcessFlowAsync(CancellationToken cancellationToken)
    {
        if (await db.ProcessStages.AnyAsync(cancellationToken))
        {
            return;
        }

        var file = Read<ProcessFlowFile>("process-flow.json");

        var stagesByOrder = file.Stages.ToDictionary(
            s => s.Order,
            s => new ProcessStage { Order = s.Order, Title = s.Title, ShortTitle = s.ShortTitle });

        var stepsByCode = file.Steps.ToDictionary(
            s => s.Code,
            s => new ProcessStep
            {
                ProcessStage = stagesByOrder.GetValueOrDefault(s.StageOrder)
                    ?? throw new InvalidOperationException($"Step {s.Code}: stage {s.StageOrder} does not exist."),
                Code = s.Code,
                Order = s.Order,
                Text = s.Text,
                IsDecision = s.IsDecision,
                NoBranchText = s.NoBranchText,
                ResponsibleRole = s.ResponsibleRole,
                ApproverRole = s.ApproverRole,
                NoOutcome = s.NoOutcome,
            });

        foreach (var stepSeed in file.Steps)
        {
            foreach (var jump in stepSeed.NoJumps)
            {
                stepsByCode[stepSeed.Code].NoJumps.Add(new ProcessStepJump
                {
                    TargetStep = FindStep(stepsByCode, jump.TargetStepCode, $"jump from {stepSeed.Code}"),
                    Label = jump.Label,
                });
            }
        }

        db.ProcessStages.AddRange(stagesByOrder.Values);
        db.ProcessSteps.AddRange(stepsByCode.Values);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Stages} process stages and {Steps} steps.",
            stagesByOrder.Count, stepsByCode.Count);
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
        var steps = await db.ProcessSteps.OrderBy(s => s.Order).ToListAsync(cancellationToken);
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
                CurrentStepId = isActive ? markerStep.Id : null,
                StartDate = seed.StartDate,
                DueDate = seed.DueDate,
                CreatedAt = ToUtc(seed.StartDate),
                CreatedById = createdById,
            };

            // History: one record per step before the marker step; closed projects also get the closing step.
            var history = steps
                .Where(s => s.Order < markerStep.Order)
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
