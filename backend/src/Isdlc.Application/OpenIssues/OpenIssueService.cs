using System.Linq.Expressions;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Domain.OpenIssues;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.OpenIssues;

public class OpenIssueService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    INotificationService notifications,
    TimeProvider timeProvider) : IOpenIssueService
{
    public async Task<PagedResult<OpenIssueListItemDto>> ListAsync(
        int projectId, OpenIssueFilter filter, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);
        var today = Today();

        var query = dbContext.OpenIssues.AsNoTracking().Where(x => x.ProjectId == projectId);

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(x => x.Type == type);
        }

        if (filter.OverdueOnly)
        {
            query = query.Where(x => x.Status != OpenIssueStatus.Closed && x.DueDate != null && x.DueDate < today);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Status == OpenIssueStatus.Closed ? 1 : 0)
            .ThenBy(x => x.DueDate == null ? 1 : 0)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Code.Length)
            .ThenBy(x => x.Code)
            .ApplyPaging(pageRequest)
            .Select(ToListItemDto(today))
            .ToListAsync(cancellationToken);

        return new PagedResult<OpenIssueListItemDto>(items, totalCount);
    }

    public async Task<OpenIssueDto> GetAsync(int projectId, int openIssueId, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        return await dbContext.OpenIssues
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Id == openIssueId)
            .Select(ToDto(Today()))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Açık konu bulunamadı.");
    }

    public async Task<OpenIssueDto> CreateAsync(
        int projectId, CreateOpenIssueDto request, CancellationToken cancellationToken)
    {
        var userId = RequireSignedIn();
        var project = await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        await ValidateOwnerAsync(request.OwnerUserId, request.OwnerName, cancellationToken);

        var highestCode = await dbContext.OpenIssues
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.Code.Length)
            .ThenByDescending(x => x.Code)
            .Select(x => x.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var issue = new OpenIssue
        {
            ProjectId = projectId,
            Code = OpenIssueRules.FormatCode(OpenIssueRules.CodePrefix, OpenIssueRules.NextCodeNumber(highestCode)),
            Status = OpenIssueStatus.Open,
        };
        ApplyContent(issue, request);
        dbContext.OpenIssues.Add(issue);

        await SaveNewAsync(issue, cancellationToken);
        await NotifyOwnerAsync(project, issue, previousOwnerUserId: null, userId, cancellationToken);

        return await GetAsync(projectId, issue.Id, cancellationToken);
    }

    public async Task<OpenIssueDto> UpdateAsync(
        int projectId, int openIssueId, UpdateOpenIssueDto request, CancellationToken cancellationToken)
    {
        var userId = RequireSignedIn();
        var project = await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        var issue = await LoadForUpdateAsync(projectId, openIssueId, cancellationToken);
        await ValidateOwnerAsync(request.OwnerUserId, request.OwnerName, cancellationToken);

        var previousOwnerUserId = issue.OwnerUserId;
        ApplyContent(issue, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyOwnerAsync(project, issue, previousOwnerUserId, userId, cancellationToken);

        return await GetAsync(projectId, issue.Id, cancellationToken);
    }

    public async Task<OpenIssueDto> UpdateStatusAsync(
        int projectId, int openIssueId, UpdateOpenIssueStatusDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        var issue = await LoadForUpdateAsync(projectId, openIssueId, cancellationToken);

        // [Required] on the DTO guarantees a value.
        OpenIssueRules.ApplyStatus(issue, request.Status!.Value, timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(projectId, issue.Id, cancellationToken);
    }

    // ---- Helpers ----

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private int RequireSignedIn() =>
        currentUser.UserId ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

    private async Task<OpenIssue> LoadForUpdateAsync(int projectId, int openIssueId, CancellationToken cancellationToken) =>
        await dbContext.OpenIssues.SingleOrDefaultAsync(
            x => x.ProjectId == projectId && x.Id == openIssueId, cancellationToken)
        ?? throw new NotFoundException("Açık konu bulunamadı.");

    /// <summary>Exactly one owner (400), and an owner user must be an active user (400).</summary>
    private async Task ValidateOwnerAsync(int? ownerUserId, string? ownerName, CancellationToken cancellationToken)
    {
        if (OpenIssueRules.ValidateOwner(ownerUserId, ownerName) is { } error)
        {
            throw new BusinessRuleException(error);
        }

        if (ownerUserId is { } id
            && !await dbContext.Users.AnyAsync(u => u.Id == id && u.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Seçilen sorumlu aktif bir kullanıcı değil.");
        }
    }

    private static void ApplyContent(OpenIssue issue, CreateOpenIssueDto request)
    {
        // [Required] on the DTO guarantees Type and Title.
        issue.Type = request.Type!.Value;
        issue.Title = request.Title!.Trim();
        issue.Description = OpenIssueRules.Clean(request.Description);
        issue.OwnerUserId = request.OwnerUserId;
        issue.OwnerName = request.OwnerUserId is null ? OpenIssueRules.Clean(request.OwnerName) : null;
        issue.DueDate = request.DueDate;
    }

    /// <summary>
    /// Saves a new record. Codes come from "highest + 1", so two records saved at the same moment
    /// can get the same code; the unique index stops the second one and the user is asked to try again.
    /// </summary>
    private async Task SaveNewAsync(OpenIssue issue, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var codeTaken = await dbContext.OpenIssues
                .AsNoTracking()
                .AnyAsync(x => x.ProjectId == issue.ProjectId && x.Code == issue.Code, cancellationToken);

            if (codeTaken)
            {
                throw new ConflictException("Kayıt numarası çakıştı. Lütfen tekrar deneyin.");
            }

            throw;
        }
    }

    /// <summary>Tells a team member that the record was assigned to them (not when they did it themselves).</summary>
    private async Task NotifyOwnerAsync(
        ProjectRef project, OpenIssue issue, int? previousOwnerUserId, int actingUserId,
        CancellationToken cancellationToken)
    {
        if (issue.OwnerUserId is { } ownerId && ownerId != previousOwnerUserId && ownerId != actingUserId)
        {
            await notifications.NotifyUsersAsync(
                [ownerId], project.Id, $"{project.Code} · {issue.Code} size atandı: {issue.Title}", cancellationToken);
        }
    }

    private static Expression<Func<OpenIssue, OpenIssueListItemDto>> ToListItemDto(DateOnly today) =>
        x => new OpenIssueListItemDto(
            x.Id,
            x.Code,
            x.Type,
            x.Title,
            new OwnerDto(x.OwnerUserId, x.OwnerUser != null ? x.OwnerUser.FullName : x.OwnerName!),
            x.DueDate,
            x.Status,
            x.Status != OpenIssueStatus.Closed && x.DueDate != null && x.DueDate < today);

    private Expression<Func<OpenIssue, OpenIssueDto>> ToDto(DateOnly today) =>
        x => new OpenIssueDto(
            x.Id,
            x.ProjectId,
            x.Code,
            x.Type,
            x.Title,
            x.Description,
            new OwnerDto(x.OwnerUserId, x.OwnerUser != null ? x.OwnerUser.FullName : x.OwnerName!),
            x.DueDate,
            x.Status,
            x.Status != OpenIssueStatus.Closed && x.DueDate != null && x.DueDate < today,
            x.ClosedAt,
            dbContext.Users
                .Where(u => u.Id == x.CreatedById)
                .Select(u => new UserSummaryDto(u.Id, u.FullName))
                .First(),
            x.CreatedAt,
            x.UpdatedAt);
}
