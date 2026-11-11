using Isdlc.Application.Assessment;
using Isdlc.Application.Assessment.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Assessment;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Assessment;

/// <summary>
/// Level and acceptance scoring on a real database. Every test creates its own project (TestProjects),
/// so seed projects are never changed. Expectations: docs/assessment-api.md
/// </summary>
[Collection("Database")]
public class AssessmentServiceTests(DatabaseFixture database)
{
    // The contract's examples: level score 12 → level 2; weighted score 3.70 → Accept.
    private static readonly SaveLevelAnswersDto ContractLevelAnswers = new()
    {
        Effort = 2, ScreensAndModules = 2, Integrations = 3, UserRoles = 2, DataSensitivity = 2, HardwareDependency = 1,
    };

    private static readonly SaveAcceptanceScoresDto ContractScores = new()
    {
        StrategicFit = 4, CommercialValue = 4, TechnicalFeasibility = 3, ResourceAndSchedule = 3, CustomerFit = 5, RiskLevel = 3,
    };

    private async Task<T> AsAsync<T>(string? email, Func<IAssessmentService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new AssessmentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId));
        return await action(service);
    }

    private Task<ProjectAssessmentDto> GetAsync(int projectId) =>
        AsAsync(TestProjects.Analyst, s => s.GetAsync(projectId, default));

    private Task<ProjectAssessmentDto> SaveLevelAsync(int projectId, SaveLevelAnswersDto answers, string email = TestProjects.Analyst) =>
        AsAsync(email, s => s.SaveLevelAnswersAsync(projectId, answers, default));

    private Task<ProjectAssessmentDto> SaveScoresAsync(int projectId, SaveAcceptanceScoresDto scores, string email = TestProjects.Analyst) =>
        AsAsync(email, s => s.SaveAcceptanceScoresAsync(projectId, scores, default));

    private Task<ProjectAssessmentDto> ApplyLevelAsync(int projectId) =>
        AsAsync(TestProjects.Analyst, s => s.ApplyLevelAsync(projectId, default));

    // ---- Get ----

    [Fact]
    public async Task Get_ProjectWithoutAssessment_ReturnsEmptySections()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        var result = await GetAsync(projectId);

        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal(0, result.Level.AnsweredCount);
        Assert.Null(result.Level.Score);
        Assert.Null(result.Level.SuggestedLevel);
        Assert.Null(result.Level.AppliedLevel);
        Assert.Equal(0, result.Acceptance.AnsweredCount);
        Assert.Null(result.Acceptance.WeightedScore);
        Assert.Null(result.Acceptance.Recommendation);
        Assert.Null(result.UpdatedBy);
        Assert.Null(result.UpdatedAt);
    }

    [Fact]
    public async Task Get_UnknownProject_Returns404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(int.MaxValue));
    }

    [Fact]
    public async Task Get_ClosedProject_CanStillBeRead()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);

        var result = await GetAsync(projectId);

        Assert.Equal(projectId, result.ProjectId);
    }

    // ---- Level answers ----

    [Fact]
    public async Task SaveLevel_HalfAnswered_IsKeptWithoutScoreOrSuggestion()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        var result = await SaveLevelAsync(projectId, new SaveLevelAnswersDto { Effort = 2, Integrations = 3, UserRoles = 1 });

        Assert.Equal(3, result.Level.AnsweredCount);
        Assert.Equal(2, result.Level.Answers.Effort);
        Assert.Null(result.Level.Answers.ScreensAndModules);
        Assert.Null(result.Level.Score);
        Assert.Null(result.Level.SuggestedLevel);
    }

    [Fact]
    public async Task SaveLevel_AllAnswered_ReturnsScoreAndSuggestedLevel()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        var result = await SaveLevelAsync(projectId, ContractLevelAnswers);

        Assert.Equal(6, result.Level.AnsweredCount);
        Assert.Equal(12, result.Level.Score);
        Assert.Equal(2, result.Level.SuggestedLevel);
    }

    [Fact]
    public async Task SaveLevel_DoesNotChangeTheProjectLevel()
    {
        // The level changes only with "Seviyeyi uygula".
        var projectId = await TestProjects.CreateAsync(database, "5");

        var result = await SaveLevelAsync(projectId, ContractLevelAnswers);

        Assert.Null(result.Level.AppliedLevel);
        Assert.Null((await TestProjects.GetAsync(database, projectId)).Level);
    }

    [Fact]
    public async Task SaveLevel_SecondSave_ReplacesTheAnswers()
    {
        // PUT: a question sent empty becomes unanswered again.
        var projectId = await TestProjects.CreateAsync(database, "5");
        await SaveLevelAsync(projectId, ContractLevelAnswers);

        var result = await SaveLevelAsync(projectId, ContractLevelAnswers with { Effort = 3, HardwareDependency = null });

        Assert.Equal(3, result.Level.Answers.Effort);
        Assert.Null(result.Level.Answers.HardwareDependency);
        Assert.Equal(5, result.Level.AnsweredCount);
        Assert.Null(result.Level.SuggestedLevel);
    }

    [Fact]
    public async Task SaveLevel_KeepsTheAcceptanceScores()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");
        await SaveScoresAsync(projectId, ContractScores);

        var result = await SaveLevelAsync(projectId, ContractLevelAnswers);

        Assert.Equal(6, result.Acceptance.AnsweredCount);
        Assert.Equal(3.70m, result.Acceptance.WeightedScore);
    }

    // ---- Acceptance scores ----

    [Fact]
    public async Task SaveScores_AllScored_ReturnsWeightedScoreAndRecommendation()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");

        var result = await SaveScoresAsync(projectId, ContractScores);

        Assert.Equal(6, result.Acceptance.AnsweredCount);
        Assert.Equal(3.70m, result.Acceptance.WeightedScore);
        Assert.Equal(AcceptanceRecommendation.Accept, result.Acceptance.Recommendation);
    }

    [Fact]
    public async Task SaveScores_LowScores_RecommendRejectOrPostpone()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");

        var result = await SaveScoresAsync(projectId, new SaveAcceptanceScoresDto
        {
            StrategicFit = 2, CommercialValue = 2, TechnicalFeasibility = 3, ResourceAndSchedule = 2, CustomerFit = 2, RiskLevel = 1,
        });

        Assert.Equal(2.10m, result.Acceptance.WeightedScore);
        Assert.Equal(AcceptanceRecommendation.RejectOrPostpone, result.Acceptance.Recommendation);
    }

    [Fact]
    public async Task SaveScores_HalfScored_IsKeptWithoutRecommendation()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");

        var result = await SaveScoresAsync(projectId, new SaveAcceptanceScoresDto { StrategicFit = 5, RiskLevel = 1 });

        Assert.Equal(2, result.Acceptance.AnsweredCount);
        Assert.Null(result.Acceptance.WeightedScore);
        Assert.Null(result.Acceptance.Recommendation);
    }

    [Fact]
    public async Task SaveScores_KeepsTheLevelAnswers()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");
        await SaveLevelAsync(projectId, ContractLevelAnswers);

        var result = await SaveScoresAsync(projectId, ContractScores);

        Assert.Equal(6, result.Level.AnsweredCount);
        Assert.Equal(12, result.Level.Score);
    }

    // ---- One assessment per project, who changed it ----

    [Fact]
    public async Task Save_SeveralTimes_KeepsOneAssessmentPerProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        await SaveLevelAsync(projectId, ContractLevelAnswers);
        await SaveScoresAsync(projectId, ContractScores);
        await SaveLevelAsync(projectId, ContractLevelAnswers with { Effort = 1 });

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.Equal(1, await db.ProjectAssessments.CountAsync(x => x.ProjectId == projectId));
    }

    [Fact]
    public async Task Save_FillsUpdatedByAndUpdatedAt()
    {
        // Who saved comes from the audit fields, which the interceptor fills from the DI user. In the fixture that
        // user is always Elif Yılmaz (the API uses the signed-in user for both), so the person is checked as Elif.
        var projectId = await TestProjects.CreateAsync(database, "4");
        var first = await SaveLevelAsync(projectId, ContractLevelAnswers);

        var second = await SaveScoresAsync(projectId, ContractScores);

        Assert.Equal(await TestProjects.UserIdAsync(database, TestProjects.Analyst), second.UpdatedBy!.Id);
        Assert.Equal("Elif Yılmaz", second.UpdatedBy.FullName);
        Assert.NotNull(first.UpdatedAt);
        Assert.True(second.UpdatedAt >= first.UpdatedAt);
    }

    [Fact]
    public async Task Save_WritesTheChangesToTheProjectsAuditLog()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await SaveLevelAsync(projectId, ContractLevelAnswers);

        await SaveLevelAsync(projectId, ContractLevelAnswers with { Effort = 3 });

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var logs = await db.AuditLogs
            .Where(a => a.ProjectId == projectId && a.EntityName == nameof(ProjectAssessment))
            .OrderBy(a => a.Id)
            .ToListAsync();
        Assert.Equal([AuditAction.Created, AuditAction.Updated], logs.Select(l => l.Action));
        Assert.Contains("\"Effort\"", logs[1].Changes);
    }

    // ---- Who may write ----

    [Fact]
    public async Task Save_ClosedProject_Returns409AndSavesNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Rejected);

        await Assert.ThrowsAsync<ConflictException>(() => SaveLevelAsync(projectId, ContractLevelAnswers));
        await Assert.ThrowsAsync<ConflictException>(() => SaveScoresAsync(projectId, ContractScores));
        await Assert.ThrowsAsync<ConflictException>(() => ApplyLevelAsync(projectId));

        Assert.Equal(0, (await GetAsync(projectId)).Level.AnsweredCount);
    }

    [Fact]
    public async Task Save_NotSignedIn_Returns403()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AsAsync(null, s => s.SaveLevelAnswersAsync(projectId, ContractLevelAnswers, default)));
    }

    [Fact]
    public async Task Save_UnknownProject_Returns404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => SaveLevelAsync(int.MaxValue, ContractLevelAnswers));
    }

    [Fact]
    public async Task Save_OtherRole_IsAllowed()
    {
        // Decision 4: like project records, any active user may fill it in, not only the business analyst.
        var projectId = await TestProjects.CreateAsync(database, "4");

        var result = await SaveScoresAsync(projectId, ContractScores, TestProjects.Developer);

        Assert.Equal(6, result.Acceptance.AnsweredCount);
    }

    // ---- Apply level ----

    [Fact]
    public async Task ApplyLevel_NothingAnswered_Returns400()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ApplyLevelAsync(projectId));

        Assert.Equal("Seviyeyi uygulamak için bütün sorular cevaplanmalıdır.", error.Message);
    }

    [Fact]
    public async Task ApplyLevel_HalfAnswered_Returns400AndKeepsTheLevel()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await SaveLevelAsync(projectId, ContractLevelAnswers with { DataSensitivity = null });

        await Assert.ThrowsAsync<BusinessRuleException>(() => ApplyLevelAsync(projectId));

        Assert.Null((await TestProjects.GetAsync(database, projectId)).Level);
    }

    [Fact]
    public async Task ApplyLevel_AllAnswered_SetsTheProjectLevel()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await SaveLevelAsync(projectId, ContractLevelAnswers);

        var result = await ApplyLevelAsync(projectId);

        Assert.Equal(2, result.Level.AppliedLevel);
        Assert.Equal(2, (await TestProjects.GetAsync(database, projectId)).Level);
    }

    [Fact]
    public async Task ApplyLevel_AnswersChangedLater_LevelStaysUntilAppliedAgain()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await SaveLevelAsync(projectId, ContractLevelAnswers);
        await ApplyLevelAsync(projectId);

        var changed = await SaveLevelAsync(projectId, ContractLevelAnswers with { Effort = 3 });

        Assert.Equal(3, changed.Level.SuggestedLevel);
        Assert.Equal(2, changed.Level.AppliedLevel);

        var applied = await ApplyLevelAsync(projectId);

        Assert.Equal(3, applied.Level.AppliedLevel);
        Assert.Equal(3, (await TestProjects.GetAsync(database, projectId)).Level);
    }

    [Fact]
    public async Task ApplyLevel_WritesTheLevelToTheProjectsAuditLog()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await SaveLevelAsync(projectId, ContractLevelAnswers);

        await ApplyLevelAsync(projectId);

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var log = await db.AuditLogs
            .Where(a => a.ProjectId == projectId && a.EntityName == nameof(Project) && a.Action == AuditAction.Updated)
            .OrderByDescending(a => a.Id)
            .FirstAsync();
        Assert.Contains("\"Level\"", log.Changes);
    }

    // ---- Saving at the same moment ----

    [Fact]
    public async Task Save_FirstAnswersAtTheSameMoment_NeverFailsWithAnUnexpectedError()
    {
        // Two people opening the empty form and saving together: one wins, the other gets a 409 (never a 500).
        var projectId = await TestProjects.CreateAsync(database, "5");

        var attempts = Enumerable.Range(0, 6).Select(async _ =>
        {
            try
            {
                await SaveLevelAsync(projectId, ContractLevelAnswers);
                return "ok";
            }
            catch (ConflictException)
            {
                return "409";
            }
        });
        var results = await Task.WhenAll(attempts);

        Assert.Contains("ok", results);
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.Equal(1, await db.ProjectAssessments.CountAsync(x => x.ProjectId == projectId));
    }
}
