using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Application.Notifications;
using Isdlc.Application.OpenIssues;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Meetings;

public class MeetingService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    INotificationService notifications,
    TimeProvider timeProvider) : IMeetingService
{
    public const string CodePrefix = "TT";

    /// <summary>A decision becomes an open issue title, so it has the same limit.</summary>
    private const int MaxDecisionLength = 300;

    public async Task<PagedResult<MeetingListItemDto>> ListAsync(
        int projectId, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        var query = dbContext.Meetings.AsNoTracking().Where(m => m.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Code.Length)
            .ThenByDescending(m => m.Code)
            .ApplyPaging(pageRequest)
            .Select(m => new
            {
                m.Id,
                m.Code,
                m.Type,
                m.Date,
                m.ReturnDate,
                m.Participants,
                DecisionCount = dbContext.OpenIssues.Count(x => x.SourceMeetingId == m.Id && x.Type == OpenIssueType.Decision),
                ActionCount = dbContext.OpenIssues.Count(x => x.SourceMeetingId == m.Id && x.Type != OpenIssueType.Decision),
                OpenActionCount = dbContext.OpenIssues.Count(x => x.SourceMeetingId == m.Id && x.Type != OpenIssueType.Decision
                    && x.Status != OpenIssueStatus.Closed),
            })
            .ToListAsync(cancellationToken);

        // Participants are stored as JSON, so their names are joined here rather than in SQL.
        var items = rows
            .Select(m => new MeetingListItemDto(
                m.Id, m.Code, m.Type, m.Date, m.ReturnDate, MeetingRules.NamesOf(m.Participants),
                m.DecisionCount, m.ActionCount, m.OpenActionCount))
            .ToList();

        return new PagedResult<MeetingListItemDto>(items, totalCount);
    }

    public async Task<MeetingDto> GetAsync(int projectId, int meetingId, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        var meeting = await dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.Id == meetingId)
            .Select(m => new
            {
                m.Id,
                m.ProjectId,
                m.Code,
                m.Type,
                m.Date,
                m.ReturnDate,
                RecordedBy = new UserSummaryDto(m.RecordedBy.Id, m.RecordedBy.FullName),
                m.Participants,
                m.Agenda,
                m.Summary,
                m.CompletedStages,
                m.OngoingStages,
                m.NextSteps,
                m.PlannedActivities,
                m.CompletedActivities,
                CreatedBy = dbContext.Users
                    .Where(u => u.Id == m.CreatedById)
                    .Select(u => new UserSummaryDto(u.Id, u.FullName))
                    .First(),
                m.CreatedAt,
                m.UpdatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Toplantı tutanağı bulunamadı.");

        // Decisions and actions are open issues now, so they are shown with their current status.
        var records = await dbContext.OpenIssues
            .AsNoTracking()
            .Where(x => x.SourceMeetingId == meetingId)
            .OrderBy(x => x.Code.Length)
            .ThenBy(x => x.Code)
            .Select(OpenIssueChecks.ToListItemDto(Today()))
            .ToListAsync(cancellationToken);

        return new MeetingDto(
            meeting.Id,
            meeting.ProjectId,
            meeting.Code,
            meeting.Type,
            meeting.Date,
            meeting.ReturnDate,
            meeting.RecordedBy,
            meeting.Participants.Select(p => new MeetingParticipantDto { Name = p.Name, Role = p.Role }).ToList(),
            meeting.Agenda,
            meeting.Summary,
            meeting.CompletedStages,
            meeting.OngoingStages,
            meeting.NextSteps,
            meeting.PlannedActivities,
            meeting.CompletedActivities,
            records.Where(x => x.Type == OpenIssueType.Decision).ToList(),
            records.Where(x => x.Type != OpenIssueType.Decision).ToList(),
            meeting.CreatedBy,
            meeting.CreatedAt,
            meeting.UpdatedAt);
    }

    public async Task<MeetingDto> CreateAsync(
        int projectId, CreateMeetingDto request, CancellationToken cancellationToken)
    {
        var userId = RequireSignedIn();
        var project = await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        await CheckContentAsync(request, cancellationToken);
        var decisions = CleanDecisions(request.Decisions);
        var actions = request.Actions ?? [];
        for (var i = 0; i < actions.Count; i++)
        {
            await dbContext.ValidateOwnerAsync(
                actions[i].OwnerUserId, actions[i].OwnerName, cancellationToken, messagePrefix: $"{i + 1}. aksiyon: ");
        }

        var meetingNumber = await dbContext.Meetings
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.Code)
            .NextNumberAsync(cancellationToken);

        var meeting = new Meeting
        {
            ProjectId = projectId,
            Code = OpenIssueRules.FormatCode(CodePrefix, meetingNumber),
        };
        ApplyContent(meeting, request);
        dbContext.Meetings.Add(meeting);

        // Open issue codes continue the project's list: actions first, then decisions, in the order entered.
        var nextIssueNumber = await dbContext.OpenIssues
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Code)
            .NextNumberAsync(cancellationToken);
        string NextIssueCode() => OpenIssueRules.FormatCode(OpenIssueRules.CodePrefix, nextIssueNumber++);

        var issues = new List<OpenIssue>();
        foreach (var action in actions)
        {
            issues.Add(new OpenIssue
            {
                ProjectId = projectId,
                Code = NextIssueCode(),
                Type = OpenIssueType.Action,
                Title = action.Title!.Trim(), // [Required] on the DTO guarantees a title.
                OwnerUserId = action.OwnerUserId,
                OwnerName = action.OwnerUserId is null ? OpenIssueRules.Clean(action.OwnerName) : null,
                DueDate = action.DueDate,
                Status = OpenIssueStatus.Open,
                SourceMeeting = meeting,
            });
        }

        // A decision is already settled: it is recorded as closed, owned by whoever saved the minutes.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var decision in decisions)
        {
            issues.Add(new OpenIssue
            {
                ProjectId = projectId,
                Code = NextIssueCode(),
                Type = OpenIssueType.Decision,
                Title = decision,
                OwnerUserId = userId,
                DueDate = meeting.Date,
                Status = OpenIssueStatus.Closed,
                ClosedAt = now,
                SourceMeeting = meeting,
            });
        }

        dbContext.OpenIssues.AddRange(issues);

        // One save for the minutes and all its open issues: either all of them are kept or none.
        await SaveNewAsync(meeting, issues, cancellationToken);

        foreach (var issue in issues.Where(x => x.Type == OpenIssueType.Action))
        {
            await notifications.NotifyOwnerAsync(project, issue, previousOwnerUserId: null, userId, cancellationToken);
        }

        return await GetAsync(projectId, meeting.Id, cancellationToken);
    }

    public async Task<MeetingDto> UpdateAsync(
        int projectId, int meetingId, UpdateMeetingDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        var meeting = await dbContext.Meetings.SingleOrDefaultAsync(
                m => m.ProjectId == projectId && m.Id == meetingId, cancellationToken)
            ?? throw new NotFoundException("Toplantı tutanağı bulunamadı.");

        await CheckContentAsync(request, cancellationToken);
        ApplyContent(meeting, request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(projectId, meeting.Id, cancellationToken);
    }

    // ---- Helpers ----

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private int RequireSignedIn() =>
        currentUser.UserId ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

    /// <summary>400 for what the attributes cannot check: the visit fields and who wrote the minutes.</summary>
    private async Task CheckContentAsync(UpdateMeetingDto request, CancellationToken cancellationToken)
    {
        // [Required] on the DTO guarantees these values.
        MeetingRules.CheckVisitFields(
            request.Type!.Value, request.Date!.Value, request.ReturnDate, request.PlannedActivities, request.CompletedActivities);

        var recordedById = request.RecordedByUserId!.Value;
        if (!await dbContext.Users.AnyAsync(u => u.Id == recordedById && u.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Tutanağı yazan aktif bir kullanıcı olmalıdır.");
        }
    }

    private static void ApplyContent(Meeting meeting, UpdateMeetingDto request)
    {
        // [Required] on the DTO and CheckContentAsync guarantee these values.
        meeting.Type = request.Type!.Value;
        meeting.Date = request.Date!.Value;
        meeting.RecordedById = request.RecordedByUserId!.Value;
        meeting.Participants = MeetingRules.ToParticipants(request.Participants!);
        meeting.Agenda = request.Agenda!.Trim();
        meeting.Summary = OpenIssueRules.Clean(request.Summary);
        meeting.CompletedStages = OpenIssueRules.Clean(request.CompletedStages);
        meeting.OngoingStages = OpenIssueRules.Clean(request.OngoingStages);
        meeting.NextSteps = OpenIssueRules.Clean(request.NextSteps);

        var isVisit = MeetingRules.IsVisit(meeting.Type);
        meeting.ReturnDate = isVisit ? request.ReturnDate : null;
        meeting.PlannedActivities = isVisit ? OpenIssueRules.Clean(request.PlannedActivities) : null;
        meeting.CompletedActivities = isVisit ? OpenIssueRules.Clean(request.CompletedActivities) : null;
    }

    /// <summary>Skips blank lines and checks the length of the others (400 names the line).</summary>
    private static List<string> CleanDecisions(IReadOnlyList<string?>? decisions)
    {
        var cleaned = new List<string>();
        if (decisions is null)
        {
            return cleaned;
        }

        for (var i = 0; i < decisions.Count; i++)
        {
            if (OpenIssueRules.Clean(decisions[i]) is not { } text)
            {
                continue;
            }

            if (text.Length > MaxDecisionLength)
            {
                throw new BusinessRuleException($"{i + 1}. karar en fazla {MaxDecisionLength} karakter olabilir.");
            }

            cleaned.Add(text);
        }

        return cleaned;
    }

    /// <summary>
    /// Saves the minutes and its open issues together. Codes come from "highest + 1", so a record saved
    /// at the same moment can take one of them; the unique index stops this save and the user tries again.
    /// </summary>
    private async Task SaveNewAsync(Meeting meeting, List<OpenIssue> issues, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var issueCodes = issues.Select(x => x.Code).ToList();
            var codeTaken =
                await dbContext.Meetings.AsNoTracking()
                    .AnyAsync(m => m.ProjectId == meeting.ProjectId && m.Code == meeting.Code, cancellationToken)
                || await dbContext.OpenIssues.AsNoTracking()
                    .AnyAsync(x => x.ProjectId == meeting.ProjectId && issueCodes.Contains(x.Code), cancellationToken);

            if (codeTaken)
            {
                throw new ConflictException("Kayıt numarası çakıştı. Lütfen tekrar deneyin.");
            }

            throw;
        }
    }
}
