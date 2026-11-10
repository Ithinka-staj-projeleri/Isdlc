using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Audit;

public class AuditService(IApplicationDbContext dbContext) : IAuditService
{
    // EntityName values written by AuditLogInterceptor (the entity class names).
    private const string ProjectEntity = "Project";
    private const string OpenIssueEntity = "OpenIssue";
    private const string MeetingEntity = "Meeting";
    private const string RequirementEntity = "Requirement";
    private const string ProjectDocumentEntity = "ProjectDocument";

    public async Task<PagedResult<AuditLogDto>> ListProjectLogAsync(
        int projectId, AuditLogFilter filter, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        var query = dbContext.AuditLogs.AsNoTracking().Where(a => a.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(a => a.EntityName == filter.EntityType);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ApplyPaging(pageRequest)
            .Select(a => new
            {
                a.Id,
                a.EntityName,
                a.EntityId,
                a.Action,
                a.Changes,
                PerformedBy = new UserSummaryDto(a.User.Id, a.User.FullName),
                a.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var fieldsByRow = rows.ToDictionary(r => r.Id, r => AuditChangeReader.Read(r.Changes));
        var allFields = fieldsByRow.Values.SelectMany(f => f).ToList();
        var names = await LoadReferenceNamesAsync(allFields, cancellationToken);
        var codes = await LoadRecordCodesAsync(rows.Select(r => (r.EntityName, r.EntityId)).ToList(), cancellationToken);

        var items = rows.Select(r => new AuditLogDto(
                r.Id,
                r.EntityName,
                r.EntityId,
                codes.GetValueOrDefault((r.EntityName, r.EntityId)),
                r.Action,
                fieldsByRow[r.Id]
                    .Select(f => new AuditFieldChangeDto(f.Name, names.Readable(f.Name, f.Old), names.Readable(f.Name, f.New)))
                    .ToList(),
                r.PerformedBy,
                r.CreatedAt))
            .ToList();

        return new PagedResult<AuditLogDto>(items, totalCount);
    }

    /// <summary>
    /// Names for the ids in reference fields of this page, loaded with one query per kind.
    /// A record that cannot be found keeps its id, so nothing in the history is hidden.
    /// </summary>
    private async Task<ReferenceNames> LoadReferenceNamesAsync(
        IReadOnlyList<AuditChangeReader.Field> fields, CancellationToken cancellationToken)
    {
        List<int> IdsOf(Func<string, bool> isField) => fields
            .Where(f => isField(f.Name))
            .SelectMany(f => new[] { AuditChangeReader.IdOf(f.Old), AuditChangeReader.IdOf(f.New) })
            .OfType<int>()
            .Distinct()
            .ToList();

        var userIds = IdsOf(AuditChangeReader.UserFields.Contains);
        var stepIds = IdsOf(name => name == AuditChangeReader.StepField);
        var meetingIds = IdsOf(name => name == AuditChangeReader.MeetingField);
        var documentTypeIds = IdsOf(name => name == AuditChangeReader.DocumentTypeField);

        return new ReferenceNames(
            Users: userIds.Count == 0 ? [] : await dbContext.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken),
            Steps: stepIds.Count == 0 ? [] : await dbContext.ProcessSteps.AsNoTracking()
                .Where(s => stepIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken),
            Meetings: meetingIds.Count == 0 ? [] : await dbContext.Meetings.AsNoTracking()
                .Where(m => meetingIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code, cancellationToken),
            DocumentTypes: documentTypeIds.Count == 0 ? [] : await dbContext.DocumentTypes.AsNoTracking()
                .Where(d => documentTypeIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken));
    }

    /// <summary>The code users know each record by: AK-003, TT-002, the project code, the document's name.</summary>
    private async Task<Dictionary<(string Entity, int Id), string>> LoadRecordCodesAsync(
        IReadOnlyList<(string Entity, int Id)> records, CancellationToken cancellationToken)
    {
        List<int> IdsOf(string entity) => records.Where(r => r.Entity == entity).Select(r => r.Id).Distinct().ToList();

        var codes = new Dictionary<(string, int), string>();
        void Add(string entity, Dictionary<int, string> byId)
        {
            foreach (var (id, code) in byId)
            {
                codes[(entity, id)] = code;
            }
        }

        if (IdsOf(ProjectEntity) is { Count: > 0 } projectIds)
        {
            Add(ProjectEntity, await dbContext.Projects.AsNoTracking()
                .Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken));
        }

        if (IdsOf(OpenIssueEntity) is { Count: > 0 } issueIds)
        {
            Add(OpenIssueEntity, await dbContext.OpenIssues.AsNoTracking()
                .Where(x => issueIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken));
        }

        if (IdsOf(MeetingEntity) is { Count: > 0 } meetingIds)
        {
            Add(MeetingEntity, await dbContext.Meetings.AsNoTracking()
                .Where(m => meetingIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code, cancellationToken));
        }

        if (IdsOf(RequirementEntity) is { Count: > 0 } requirementIds)
        {
            Add(RequirementEntity, await dbContext.Requirements.AsNoTracking()
                .Where(r => requirementIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Code, cancellationToken));
        }

        if (IdsOf(ProjectDocumentEntity) is { Count: > 0 } documentIds)
        {
            // Projected first: ToDictionaryAsync's selectors run in memory, where DocumentType is not loaded.
            Add(ProjectDocumentEntity, await dbContext.ProjectDocuments.AsNoTracking()
                .Where(d => documentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.DocumentType.Name })
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken));
        }

        return codes;
    }

    private sealed record ReferenceNames(
        Dictionary<int, string> Users,
        Dictionary<int, string> Steps,
        Dictionary<int, string> Meetings,
        Dictionary<int, string> DocumentTypes)
    {
        /// <summary>The value as people read it: a name or code for a reference field, otherwise unchanged.</summary>
        public string? Readable(string field, string? value)
        {
            if (AuditChangeReader.IdOf(value) is not { } id)
            {
                return value;
            }

            // Rows written before the interceptor fix may hold EF's temporary negative id for a record saved
            // in the same transaction; it never pointed to a real record, so it is shown as unknown.
            if (id < 0 && IsReference(field))
            {
                return null;
            }

            var names = field switch
            {
                _ when AuditChangeReader.UserFields.Contains(field) => Users,
                AuditChangeReader.StepField => Steps,
                AuditChangeReader.MeetingField => Meetings,
                AuditChangeReader.DocumentTypeField => DocumentTypes,
                _ => null,
            };

            return names is not null && names.TryGetValue(id, out var name) ? name : value;
        }

        private static bool IsReference(string field) =>
            AuditChangeReader.UserFields.Contains(field)
            || field is AuditChangeReader.StepField or AuditChangeReader.MeetingField or AuditChangeReader.DocumentTypeField;
    }
}
