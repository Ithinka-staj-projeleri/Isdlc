using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Audit;

/// <summary>
/// A project's timeline, put together from its process records, minutes, decisions and the status changes in its
/// audit log. Nothing is stored for it. Contract: docs/audit-api.md → Zaman çizelgesi.
/// </summary>
public class TimelineService(IApplicationDbContext dbContext) : ITimelineService
{
    private const string RequirementEntity = "Requirement";
    private const string RiskEntity = "Risk";
    private const string StatusField = "Status";

    public async Task<PagedResult<TimelineEventDto>> ListProjectTimelineAsync(
        int projectId, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        // A project has at most a few hundred events, so they are gathered and paged in memory.
        var events = new List<TimelineEventDto>();
        events.Add(await CreatedAsync(projectId, cancellationToken));
        events.AddRange(await StepsAsync(projectId, cancellationToken));
        events.AddRange(await MeetingsAsync(projectId, cancellationToken));
        events.AddRange(await DecisionsAsync(projectId, cancellationToken));
        events.AddRange(await StatusChangesAsync(projectId, cancellationToken));

        var items = events
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Kind)
            .Skip((pageRequest.Page - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToList();

        return new PagedResult<TimelineEventDto>(items, events.Count);
    }

    private async Task<TimelineEventDto> CreatedAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                p.Code,
                p.Name,
                p.CreatedAt,
                // Audit fields keep only the id; the name comes from a subquery.
                CreatedBy = dbContext.Users.Where(u => u.Id == p.CreatedById).Select(u => new UserSummaryDto(u.Id, u.FullName)).First(),
            })
            .SingleAsync(cancellationToken);

        return new TimelineEventDto(
            TimelineEventKind.Created, project.CreatedAt, false, project.CreatedBy, null, project.Code, project.Name,
            null, null, null, null);
    }

    private async Task<IEnumerable<TimelineEventDto>> StepsAsync(int projectId, CancellationToken cancellationToken)
    {
        var records = await dbContext.StepRecords
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .Select(r => new
            {
                r.Action,
                StepCode = r.ProcessStep.Code,
                StepText = r.ProcessStep.Text,
                JumpedToStepCode = r.JumpedToStep != null ? r.JumpedToStep.Code : null,
                r.Note,
                PerformedBy = new UserSummaryDto(r.PerformedBy.Id, r.PerformedBy.FullName),
                r.PerformedAt,
            })
            .ToListAsync(cancellationToken);

        return records.Select(r => new TimelineEventDto(
            TimelineEventKind.Step, r.PerformedAt, false, r.PerformedBy, null, r.StepCode,
            TimelineText.FirstSentence(r.StepText), r.Action, r.JumpedToStepCode, null, r.Note));
    }

    private async Task<IEnumerable<TimelineEventDto>> MeetingsAsync(int projectId, CancellationToken cancellationToken)
    {
        var meetings = await dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .Select(m => new
            {
                m.Id,
                m.Code,
                m.Type,
                m.Date,
                m.Agenda,
                m.Summary,
                RecordedBy = new UserSummaryDto(m.RecordedBy.Id, m.RecordedBy.FullName),
            })
            .ToListAsync(cancellationToken);

        return meetings.Select(m => new TimelineEventDto(
            TimelineEventKind.Meeting, m.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), true, m.RecordedBy, m.Id,
            m.Code, TimelineText.FirstAgendaLine(m.Agenda), null, null, m.Type, m.Summary));
    }

    private async Task<IEnumerable<TimelineEventDto>> DecisionsAsync(int projectId, CancellationToken cancellationToken)
    {
        var decisions = await dbContext.OpenIssues
            .AsNoTracking()
            .Where(i => i.ProjectId == projectId && i.Type == OpenIssueType.Decision)
            .Select(i => new
            {
                i.Id,
                i.Code,
                i.Title,
                i.CreatedAt,
                CreatedBy = dbContext.Users.Where(u => u.Id == i.CreatedById).Select(u => new UserSummaryDto(u.Id, u.FullName)).First(),
            })
            .ToListAsync(cancellationToken);

        return decisions.Select(d => new TimelineEventDto(
            TimelineEventKind.Decision, d.CreatedAt, false, d.CreatedBy, d.Id, d.Code, d.Title, null, null, null, null));
    }

    /// <summary>
    /// Requirements that became approved and risks that were closed. The moment comes from the audit log: the records
    /// themselves only know their current status. A record approved twice shows twice; both approvals happened.
    /// </summary>
    private async Task<IEnumerable<TimelineEventDto>> StatusChangesAsync(int projectId, CancellationToken cancellationToken)
    {
        var logs = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId
                && (a.EntityName == RequirementEntity || a.EntityName == RiskEntity)
                && a.Changes.Contains("\"" + StatusField + "\""))
            .Select(a => new
            {
                a.EntityName,
                a.EntityId,
                a.Changes,
                PerformedBy = new UserSummaryDto(a.User.Id, a.User.FullName),
                a.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var changes = logs
            .Select(log => (Log: log, Kind: KindOf(log.EntityName, AuditChangeReader.Read(log.Changes))))
            .Where(c => c.Kind is not null)
            .ToList();
        if (changes.Count == 0)
        {
            return [];
        }

        var requirementIds = changes.Where(c => c.Kind == TimelineEventKind.RequirementApproved).Select(c => c.Log.EntityId).ToList();
        var riskIds = changes.Where(c => c.Kind == TimelineEventKind.RiskClosed).Select(c => c.Log.EntityId).ToList();
        var requirements = await dbContext.Requirements
            .AsNoTracking()
            .Where(r => requirementIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => (r.Code, r.Title), cancellationToken);
        var risks = await dbContext.Risks
            .AsNoTracking()
            .Where(r => riskIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => (r.Code, r.Title), cancellationToken);

        return changes
            .Select(c =>
            {
                var records = c.Kind == TimelineEventKind.RequirementApproved ? requirements : risks;
                var (code, title) = records.GetValueOrDefault(c.Log.EntityId, (c.Log.EntityId.ToString(), string.Empty));
                return new TimelineEventDto(
                    c.Kind!.Value, c.Log.CreatedAt, false, c.Log.PerformedBy, c.Log.EntityId, code, title, null, null, null, null);
            })
            .ToList();
    }

    /// <summary>The event a status change is, or null when it is not one the timeline shows.</summary>
    private static TimelineEventKind? KindOf(string entityName, IReadOnlyList<AuditChangeReader.Field> fields)
    {
        var status = fields.FirstOrDefault(f => f.Name == StatusField);
        if (status is null || status.New == status.Old)
        {
            return null;
        }

        return entityName switch
        {
            RequirementEntity when status.New == nameof(RequirementStatus.Approved) => TimelineEventKind.RequirementApproved,
            RiskEntity when status.New == nameof(RiskStatus.Closed) => TimelineEventKind.RiskClosed,
            _ => null,
        };
    }
}
