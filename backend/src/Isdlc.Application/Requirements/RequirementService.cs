using System.Linq.Expressions;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.Requirements.Dtos;
using Isdlc.Domain.Requirements;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Requirements;

public class RequirementService(IApplicationDbContext dbContext, ICurrentUserService currentUser) : IRequirementService
{
    public const string CodePrefix = "GR";

    public async Task<PagedResult<RequirementListItemDto>> ListAsync(
        int projectId, RequirementFilter filter, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        var query = dbContext.Requirements.AsNoTracking().Where(x => x.ProjectId == projectId);

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(x => x.Type == type);
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(x => x.Priority == priority);
        }

        if (OpenIssueRules.Clean(filter.Search) is { } search)
        {
            query = query.Where(x => x.Code.Contains(search) || x.Title.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Status == RequirementStatus.Cancelled ? 1 : 0)
            // Shorter codes first, so GR-999 comes before GR-1000.
            .ThenBy(x => x.Code.Length)
            .ThenBy(x => x.Code)
            .ApplyPaging(pageRequest)
            .Select(x => new RequirementListItemDto(x.Id, x.Code, x.Type, x.Title, x.Priority, x.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<RequirementListItemDto>(items, totalCount);
    }

    public async Task<RequirementDto> GetAsync(int projectId, int requirementId, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        return await dbContext.Requirements
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Id == requirementId)
            .Select(ToDto())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Gereksinim bulunamadı.");
    }

    public async Task<RequirementDto> CreateAsync(
        int projectId, CreateRequirementDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        var number = await dbContext.Requirements
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Code)
            .NextNumberAsync(cancellationToken);

        var requirement = new Requirement
        {
            ProjectId = projectId,
            Code = OpenIssueRules.FormatCode(CodePrefix, number),
            Status = RequirementStatus.Draft,
        };
        ApplyContent(requirement, request);
        dbContext.Requirements.Add(requirement);

        await SaveNewAsync(requirement, cancellationToken);

        return await GetAsync(projectId, requirement.Id, cancellationToken);
    }

    public async Task<RequirementDto> UpdateAsync(
        int projectId, int requirementId, UpdateRequirementDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        var requirement = await LoadForUpdateAsync(projectId, requirementId, cancellationToken);

        ApplyContent(requirement, request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(projectId, requirement.Id, cancellationToken);
    }

    public async Task<RequirementDto> UpdateStatusAsync(
        int projectId, int requirementId, UpdateRequirementStatusDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        var requirement = await LoadForUpdateAsync(projectId, requirementId, cancellationToken);

        // [Required] on the DTO guarantees a value.
        requirement.Status = request.Status!.Value;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(projectId, requirement.Id, cancellationToken);
    }

    // ---- Helpers ----

    private int RequireSignedIn() =>
        currentUser.UserId ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

    private async Task<Requirement> LoadForUpdateAsync(
        int projectId, int requirementId, CancellationToken cancellationToken) =>
        await dbContext.Requirements.SingleOrDefaultAsync(
            x => x.ProjectId == projectId && x.Id == requirementId, cancellationToken)
        ?? throw new NotFoundException("Gereksinim bulunamadı.");

    private static void ApplyContent(Requirement requirement, CreateRequirementDto request)
    {
        // [Required] on the DTO guarantees Type, Title and Priority.
        var title = request.Title!.Trim();
        if (title.Length == 0)
        {
            throw new BusinessRuleException("Başlık boş olamaz.");
        }

        requirement.Type = request.Type!.Value;
        requirement.Title = title;
        requirement.Description = OpenIssueRules.Clean(request.Description);
        requirement.AcceptanceCriteria = OpenIssueRules.Clean(request.AcceptanceCriteria);
        requirement.Priority = request.Priority!.Value;
    }

    /// <summary>
    /// Saves a new record. Codes come from "highest + 1", so two records saved at the same moment
    /// can get the same code; the unique index stops the second one and the user is asked to try again.
    /// </summary>
    private async Task SaveNewAsync(Requirement requirement, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var codeTaken = await dbContext.Requirements
                .AsNoTracking()
                .AnyAsync(x => x.ProjectId == requirement.ProjectId && x.Code == requirement.Code, cancellationToken);

            if (codeTaken)
            {
                throw new ConflictException("Kayıt numarası çakıştı. Lütfen tekrar deneyin.");
            }

            throw;
        }
    }

    private Expression<Func<Requirement, RequirementDto>> ToDto() =>
        x => new RequirementDto(
            x.Id,
            x.ProjectId,
            x.Code,
            x.Type,
            x.Title,
            x.Description,
            x.AcceptanceCriteria,
            x.Priority,
            x.Status,
            dbContext.Users
                .Where(u => u.Id == x.CreatedById)
                .Select(u => new UserSummaryDto(u.Id, u.FullName))
                .First(),
            x.CreatedAt,
            x.UpdatedAt);
}
