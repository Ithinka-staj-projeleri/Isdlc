using Isdlc.Application.Assessment.Dtos;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Assessment;

public class AssessmentService(IApplicationDbContext dbContext, ICurrentUserService currentUser) : IAssessmentService
{
    public AssessmentDefinitionDto GetDefinition() => AssessmentRules.Definition;

    public async Task<ProjectAssessmentDto> GetAsync(int projectId, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        var appliedLevel = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => p.Level)
            .SingleAsync(cancellationToken);

        var row = await dbContext.ProjectAssessments
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(x => new
            {
                Assessment = x,
                UpdatedBy = dbContext.Users
                    .Where(u => u.Id == (x.UpdatedById ?? x.CreatedById))
                    .Select(u => new UserSummaryDto(u.Id, u.FullName))
                    .First(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return ToDto(projectId, row?.Assessment, appliedLevel, row?.UpdatedBy);
    }

    public async Task<ProjectAssessmentDto> SaveLevelAnswersAsync(
        int projectId, SaveLevelAnswersDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        var assessment = await LoadOrAddAsync(projectId, cancellationToken);
        assessment.Effort = request.Effort;
        assessment.ScreensAndModules = request.ScreensAndModules;
        assessment.Integrations = request.Integrations;
        assessment.UserRoles = request.UserRoles;
        assessment.DataSensitivity = request.DataSensitivity;
        assessment.HardwareDependency = request.HardwareDependency;

        await SaveAsync(assessment, cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    public async Task<ProjectAssessmentDto> SaveAcceptanceScoresAsync(
        int projectId, SaveAcceptanceScoresDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        var assessment = await LoadOrAddAsync(projectId, cancellationToken);
        assessment.StrategicFit = request.StrategicFit;
        assessment.CommercialValue = request.CommercialValue;
        assessment.TechnicalFeasibility = request.TechnicalFeasibility;
        assessment.ResourceAndSchedule = request.ResourceAndSchedule;
        assessment.CustomerFit = request.CustomerFit;
        assessment.RiskLevel = request.RiskLevel;

        await SaveAsync(assessment, cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    public async Task<ProjectAssessmentDto> ApplyLevelAsync(int projectId, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        var assessment = await dbContext.ProjectAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

        if (assessment is null || AssessmentRules.SuggestedLevel(LevelAnswersOf(assessment)) is not { } level)
        {
            throw new BusinessRuleException("Seviyeyi uygulamak için bütün sorular cevaplanmalıdır.");
        }

        var project = await dbContext.Projects.SingleAsync(p => p.Id == projectId, cancellationToken);
        project.Level = level;

        // A save at the same moment as another change to this project fails on RowVersion and returns 409.
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    // ---- Helpers ----

    private void RequireSignedIn()
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }
    }

    /// <summary>The project's assessment; a new, empty one the first time something is saved.</summary>
    private async Task<ProjectAssessment> LoadOrAddAsync(int projectId, CancellationToken cancellationToken)
    {
        var assessment = await dbContext.ProjectAssessments
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

        if (assessment is null)
        {
            assessment = new ProjectAssessment { ProjectId = projectId };
            dbContext.ProjectAssessments.Add(assessment);
        }

        return assessment;
    }

    /// <summary>
    /// Saves the assessment. If two people save the very first answers at the same moment, the unique index stops
    /// the second one; they are asked to reload instead of silently losing the other person's answers.
    /// </summary>
    private async Task SaveAsync(ProjectAssessment assessment, CancellationToken cancellationToken)
    {
        var isNew = dbContext.Entry(assessment).State == EntityState.Added;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNew)
        {
            var exists = await dbContext.ProjectAssessments
                .AsNoTracking()
                .AnyAsync(x => x.ProjectId == assessment.ProjectId, cancellationToken);

            if (exists)
            {
                throw new ConflictException("Değerlendirme bu arada başka biri tarafından kaydedildi. Sayfayı yenileyin.");
            }

            throw;
        }
    }

    private static LevelAnswersDto LevelAnswersOf(ProjectAssessment? x) => x is null
        ? new LevelAnswersDto()
        : new LevelAnswersDto
        {
            Effort = x.Effort,
            ScreensAndModules = x.ScreensAndModules,
            Integrations = x.Integrations,
            UserRoles = x.UserRoles,
            DataSensitivity = x.DataSensitivity,
            HardwareDependency = x.HardwareDependency,
        };

    private static AcceptanceScoresDto AcceptanceScoresOf(ProjectAssessment? x) => x is null
        ? new AcceptanceScoresDto()
        : new AcceptanceScoresDto
        {
            StrategicFit = x.StrategicFit,
            CommercialValue = x.CommercialValue,
            TechnicalFeasibility = x.TechnicalFeasibility,
            ResourceAndSchedule = x.ResourceAndSchedule,
            CustomerFit = x.CustomerFit,
            RiskLevel = x.RiskLevel,
        };

    private static ProjectAssessmentDto ToDto(
        int projectId, ProjectAssessment? assessment, int? appliedLevel, UserSummaryDto? updatedBy)
    {
        var answers = LevelAnswersOf(assessment);
        var scores = AcceptanceScoresOf(assessment);
        var weightedScore = AssessmentRules.WeightedScore(scores);

        return new ProjectAssessmentDto(
            projectId,
            new LevelSectionDto(
                answers,
                AssessmentRules.AnsweredCount(answers),
                AssessmentRules.LevelScore(answers),
                AssessmentRules.SuggestedLevel(answers),
                appliedLevel),
            new AcceptanceSectionDto(
                scores,
                AssessmentRules.AnsweredCount(scores),
                weightedScore,
                weightedScore is { } value ? AssessmentRules.RecommendationOf(value) : null),
            updatedBy,
            assessment is null ? null : assessment.UpdatedAt ?? assessment.CreatedAt);
    }
}
