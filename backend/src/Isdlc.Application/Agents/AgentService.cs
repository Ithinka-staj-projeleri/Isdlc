using System.Globalization;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.OpenIssues;
using Isdlc.Domain.OpenIssues;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Agents;

public class AgentService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    IMeetingAgentClient meetingAgent) : IAgentService
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<AgentStatusDto> GetStatusAsync(CancellationToken cancellationToken) =>
        new(await meetingAgent.IsAvailableAsync(cancellationToken));

    public async Task<MeetingSuggestionsDto> SuggestMeetingItemsAsync(
        int projectId, SuggestMeetingItemsDto request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        // Same rule as saving the minutes: suggestions are only useful where the minutes can be saved.
        var project = await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        if (!meetingAgent.IsConfigured)
        {
            throw new ServiceUnavailableException("Yapay zekâ servisi bu ortamda açık değil.");
        }

        // The current step tells the agent what kind of meeting this probably was (e.g. 7.1 analysis meetings).
        var currentStep = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId && p.CurrentStep != null)
            .Select(p => p.CurrentStep!.Code + " " + p.CurrentStep.Text)
            .SingleOrDefaultAsync(cancellationToken);

        // [Required] on the DTO guarantees these values.
        var result = await meetingAgent.SuggestOpenIssuesAsync(
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
