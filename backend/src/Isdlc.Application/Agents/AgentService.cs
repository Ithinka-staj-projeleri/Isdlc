using System.Globalization;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.Risks;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Agents;

public class AgentService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    IAgentClient agents) : IAgentService
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<AgentStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var available = await agents.IsAvailableAsync(cancellationToken);
        return new AgentStatusDto(available, available);
    }

    public async Task<MeetingSuggestionsDto> SuggestMeetingItemsAsync(
        int projectId, SuggestMeetingItemsDto request, CancellationToken cancellationToken)
    {
        // The current step tells the agent what kind of meeting this probably was (e.g. 7.1 analysis meetings).
        var (project, currentStep) = await PrepareAsync(projectId, cancellationToken);

        // [Required] on the DTO guarantees these values.
        var result = await agents.SuggestOpenIssuesAsync(
            new MeetingAgentRequest(project.Code, request.MeetingDate!.Value, request.Notes!.Trim(), currentStep),
            cancellationToken);

        var usersByName = await ActiveUsersByNameAsync(cancellationToken);

        var decisions = new List<SuggestedDecisionDto>();
        var actions = new List<SuggestedActionDto>();
        var others = new List<SuggestedOpenIssueDto>();
        var skipped = result.InvalidCount;

        foreach (var item in result.Items)
        {
            if (!Enum.TryParse<OpenIssueType>(item.Type, out var type) || string.IsNullOrWhiteSpace(item.Title))
            {
                skipped++;
                continue;
            }

            var title = item.Title.Trim();
            switch (type)
            {
                case OpenIssueType.Decision:
                    // A decision is saved owned by whoever saves the minutes, so the agent's owner is not used.
                    decisions.Add(new SuggestedDecisionDto(title, item.Evidence));
                    break;

                case OpenIssueType.Action:
                    var ownerName = OpenIssueRules.Clean(item.OwnerName);
                    int? ownerUserId = ownerName is not null && usersByName.TryGetValue(ownerName, out var id) ? id : null;
                    actions.Add(new SuggestedActionDto(
                        title,
                        ownerUserId,
                        ownerUserId is null ? ownerName : null,
                        item.DueDate,
                        item.Evidence,
                        item.Warnings));
                    break;

                default:
                    others.Add(new SuggestedOpenIssueDto(type, title, OpenIssueRules.Clean(item.OwnerName), item.DueDate, item.Evidence));
                    break;
            }
        }

        return new MeetingSuggestionsDto(result.Summary, decisions, actions, others, skipped, result.ModelName);
    }

    public async Task<RiskSuggestionsDto> SuggestRisksAsync(
        int projectId, SuggestRisksDto request, CancellationToken cancellationToken)
    {
        var (project, currentStep) = await PrepareAsync(projectId, cancellationToken);

        // [Required] on the DTO guarantees the value.
        var result = await agents.SuggestRisksAsync(
            new RiskAgentRequest(project.Code, request.SourceText!.Trim(), currentStep), cancellationToken);

        var items = new List<SuggestedRiskDto>();
        var skipped = result.InvalidCount;
        foreach (var item in result.Items)
        {
            var title = item.Title.Trim();
            if (title.Length is 0 or > 300)
            {
                skipped++;
                continue;
            }

            var warnings = item.Warnings.ToList();
            var (probability, impact) = CheckScore(item.Probability, item.Impact, warnings);
            items.Add(new SuggestedRiskDto(
                title,
                OpenIssueRules.Clean(item.Description),
                Enum.TryParse<RiskCategory>(item.Category, out var category) ? category : null,
                probability,
                impact,
                ScoreOf(probability, impact),
                SeverityOf(probability, impact),
                OpenIssueRules.Clean(item.AssessmentReason),
                OpenIssueRules.Clean(item.Mitigation),
                OpenIssueRules.Clean(item.Evidence),
                warnings));
        }

        return new RiskSuggestionsDto(items, skipped, result.ModelName);
    }

    public async Task<RiskAssessmentSuggestionDto> SuggestRiskAssessmentAsync(
        int projectId, SuggestRiskAssessmentDto request, CancellationToken cancellationToken)
    {
        var (project, currentStep) = await PrepareAsync(projectId, cancellationToken);

        // [Required] on the DTO guarantees the title.
        var result = await agents.AssessRiskAsync(
            new RiskAssessmentAgentRequest(
                project.Code, request.Title!.Trim(), OpenIssueRules.Clean(request.Description), currentStep),
            cancellationToken);

        var warnings = result.Warnings.ToList();
        var (probability, impact) = CheckScore(result.Probability, result.Impact, warnings);
        return new RiskAssessmentSuggestionDto(
            probability,
            impact,
            ScoreOf(probability, impact),
            SeverityOf(probability, impact),
            OpenIssueRules.Clean(result.AssessmentReason),
            OpenIssueRules.Clean(result.Mitigation),
            warnings,
            result.ModelName);
    }

    /// <summary>
    /// Checks shared by every suggestion: signed in, the project exists and is active (suggestions are only useful
    /// where the records can be saved), the service is configured. Returns the project and its current step.
    /// </summary>
    private async Task<(ProjectRef Project, string? CurrentStep)> PrepareAsync(
        int projectId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        var project = await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        if (!agents.IsConfigured)
        {
            throw new ServiceUnavailableException("Yapay zekâ servisi bu ortamda açık değil.");
        }

        var currentStep = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId && p.CurrentStep != null)
            .Select(p => p.CurrentStep!.Code + " " + p.CurrentStep.Text)
            .SingleOrDefaultAsync(cancellationToken);

        return (project, currentStep);
    }

    /// <summary>Both values 1–5, or neither: a half or out-of-range score is dropped and the person scores it.</summary>
    private static (int? Probability, int? Impact) CheckScore(int? probability, int? impact, List<string> warnings)
    {
        if (probability is null && impact is null)
        {
            return (null, null);
        }

        if (probability is >= RiskRules.MinLevel and <= RiskRules.MaxLevel
            && impact is >= RiskRules.MinLevel and <= RiskRules.MaxLevel)
        {
            return (probability, impact);
        }

        warnings.Add("Önerilen puan geçersizdi; olasılık ve etkiyi siz seçin.");
        return (null, null);
    }

    private static int? ScoreOf(int? probability, int? impact) =>
        probability is { } p && impact is { } i ? RiskRules.Score(p, i) : null;

    private static RiskSeverity? SeverityOf(int? probability, int? impact) =>
        ScoreOf(probability, impact) is { } score ? RiskRules.SeverityOf(score) : null;

    /// <summary>Active users by full name, compared the Turkish way ("İ" / "i"), so "can öztürk" finds "Can Öztürk".</summary>
    private async Task<Dictionary<string, int>> ActiveUsersByNameAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(cancellationToken);

        // Two people with the same name are left out: the person reviewing picks, the code does not guess.
        var comparer = StringComparer.Create(Turkish, ignoreCase: true);
        return users
            .GroupBy(u => u.FullName, comparer)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().Id, comparer);
    }
}
