using System.Linq.Expressions;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Projects;

public class ProjectService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    INotificationService notifications,
    TimeProvider timeProvider) : IProjectService
{
    /// <summary>The first try plus 3 retries when another request took the same code at the same moment.</summary>
    private const int MaxCodeAttempts = 4;

    /// <summary>Stages 1–3 are the request phase (up to project acceptance at 3.2); 4–13 is the project phase.</summary>
    private const int LastRequestStageOrder = 3;

    public async Task<PagedResult<ProjectListItemDto>> ListAsync(
        ProjectFilter filter, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        var query = dbContext.Projects.AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (filter.Phase == ProjectPhase.Request)
        {
            query = query.Where(p => p.CurrentStep != null && p.CurrentStep.ProcessStage.Order <= LastRequestStageOrder);
        }
        else if (filter.Phase == ProjectPhase.Project)
        {
            query = query.Where(p => p.CurrentStep != null && p.CurrentStep.ProcessStage.Order > LastRequestStageOrder);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // The database collation ignores case, so "portal" also finds "Portal".
            var search = filter.Search.Trim();
            query = query.Where(p =>
                p.Code.Contains(search) || p.Name.Contains(search) || p.CustomerName.Contains(search));
        }

        if (filter.BusinessAnalystId is { } analystId)
        {
            query = query.Where(p => p.BusinessAnalystId == analystId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // Newest request first. Longer codes first, so "-1000" comes before "-999".
            .OrderByDescending(p => p.Code.Length)
            .ThenByDescending(p => p.Code)
            .ApplyPaging(pageRequest)
            .Select(p => new ProjectListItemDto(
                p.Id,
                p.Code,
                p.Name,
                p.CustomerName,
                p.RequestType,
                p.Status,
                p.CurrentStep != null
                    ? new CurrentStepSummaryDto(
                        p.CurrentStep.Id, p.CurrentStep.Code,
                        p.CurrentStep.ProcessStage.Order, p.CurrentStep.ProcessStage.ShortTitle)
                    : null,
                p.BusinessAnalyst != null ? new UserSummaryDto(p.BusinessAnalyst.Id, p.BusinessAnalyst.FullName) : null,
                p.Level,
                p.Priority,
                p.StartDate,
                p.DueDate))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectListItemDto>(items, totalCount);
    }

    public async Task<ProjectDto> GetAsync(int projectId, CancellationToken cancellationToken)
    {
        return await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(ToDto())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Proje bulunamadı.");
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectDto request, CancellationToken cancellationToken)
    {
        var userId = RequireSignedIn();

        // Team decision: only a Business Analyst opens requests (docs/projects-api.md → Ortak kurallar).
        if (!await IsActiveUserWithRoleAsync(userId, UserRole.BusinessAnalyst, cancellationToken))
        {
            throw new ForbiddenException("Talebi yalnızca İş Analisti açabilir.");
        }

        await ValidatePeopleAsync(request.BusinessAnalystId, request.ProjectManagerId, cancellationToken);

        // Agreed with Process: a new request starts Active at the first step of the flow (1.1).
        var firstStepId = await dbContext.ProcessSteps
            .OrderBy(s => s.Order)
            .Select(s => s.Id)
            .FirstAsync(cancellationToken);

        var project = new Project
        {
            Status = ProjectStatus.Active,
            CurrentStepId = firstStepId,
        };
        ApplyContent(project, request);
        dbContext.Projects.Add(project);

        await SaveWithNewCodeAsync(project, cancellationToken);
        await NotifyAssignedPeopleAsync(project, previousAnalystId: null, previousManagerId: null, userId, cancellationToken);

        return await GetAsync(project.Id, cancellationToken);
    }

    public async Task<ProjectDto> UpdateAsync(
        int projectId, UpdateProjectDto request, CancellationToken cancellationToken)
    {
        var userId = RequireSignedIn();

        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Proje bulunamadı.");

        // Team decision: a closed, rejected, lost or redirected project keeps its information as it was.
        if (project.Status != ProjectStatus.Active)
        {
            throw new ConflictException("Proje aktif değil; bilgileri değiştirilemez.");
        }

        await ValidatePeopleAsync(request.BusinessAnalystId, request.ProjectManagerId, cancellationToken);

        var previousAnalystId = project.BusinessAnalystId;
        var previousManagerId = project.ProjectManagerId;
        ApplyContent(project, request);

        // A save at the same moment as another change to this project fails on RowVersion and returns 409.
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyAssignedPeopleAsync(project, previousAnalystId, previousManagerId, userId, cancellationToken);

        return await GetAsync(project.Id, cancellationToken);
    }

    // ---- Helpers ----

    private int RequireSignedIn() =>
        currentUser.UserId ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

    /// <summary>Copies the editable fields. Required fields are guaranteed by the DTO's attributes.</summary>
    private static void ApplyContent(Project project, CreateProjectDto request)
    {
        project.Name = request.Name!.Trim();
        project.Description = request.Description!.Trim();
        project.Source = request.Source!.Value;
        project.RequestChannel = request.RequestChannel!.Value;
        project.RequestType = request.RequestType!.Value;
        project.CustomerName = request.CustomerName!.Trim();
        project.RequesterName = request.RequesterName!.Trim();
        project.RequiresNda = request.RequiresNda;
        project.BusinessAnalystId = request.BusinessAnalystId;
        project.ProjectManagerId = request.ProjectManagerId;
        project.StartDate = request.StartDate!.Value;
        project.DueDate = request.DueDate;
    }

    /// <summary>An assigned person must be an active user with the matching role (400).</summary>
    private async Task ValidatePeopleAsync(
        int? businessAnalystId, int? projectManagerId, CancellationToken cancellationToken)
    {
        if (businessAnalystId is { } analystId && !await IsActiveUserWithRoleAsync(analystId, UserRole.BusinessAnalyst, cancellationToken))
        {
            throw new BusinessRuleException("Seçilen iş analisti aktif bir İş Analisti değil.");
        }

        if (projectManagerId is { } managerId && !await IsActiveUserWithRoleAsync(managerId, UserRole.ProjectManager, cancellationToken))
        {
            throw new BusinessRuleException("Seçilen proje yöneticisi aktif bir Proje Yöneticisi değil.");
        }
    }

    private Task<bool> IsActiveUserWithRoleAsync(int userId, UserRole role, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Role == role, cancellationToken);

    /// <summary>
    /// Gives the project the next code of the year and saves it. Codes come from "highest + 1", so two requests
    /// saved at the same moment can pick the same code; the unique index stops the second one, which then
    /// tries again with a new number. The user is never asked to resend the form.
    /// </summary>
    private async Task SaveWithNewCodeAsync(Project project, CancellationToken cancellationToken)
    {
        var year = timeProvider.GetUtcNow().Year;

        for (var attempt = 1; ; attempt++)
        {
            project.Code = ProjectCodes.Format(year, ProjectCodes.NextNumber(await HighestCodeAsync(year, cancellationToken)));

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException)
            {
                if (!await IsCodeTakenAsync(project.Code, cancellationToken))
                {
                    throw; // Not a code clash: let the real error through.
                }
                if (attempt == MaxCodeAttempts)
                {
                    throw new ConflictException("Proje bu arada değiştirildi. Sayfayı yenileyin.");
                }
                // Otherwise loop: pick the next free number and try again.
            }
        }
    }

    /// <summary>Highest code of the year. Longer codes first, so "-1000" comes before "-999".</summary>
    private Task<string?> HighestCodeAsync(int year, CancellationToken cancellationToken)
    {
        var prefix = ProjectCodes.YearPrefix(year);
        return dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Code.StartsWith(prefix))
            .OrderByDescending(p => p.Code.Length)
            .ThenByDescending(p => p.Code)
            .Select(p => p.Code)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<bool> IsCodeTakenAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking().AnyAsync(p => p.Code == code, cancellationToken);

    /// <summary>Tells newly assigned people about the project (not when they made the change themselves).</summary>
    private async Task NotifyAssignedPeopleAsync(
        Project project, int? previousAnalystId, int? previousManagerId, int actingUserId,
        CancellationToken cancellationToken)
    {
        if (project.BusinessAnalystId is { } analystId && analystId != previousAnalystId && analystId != actingUserId)
        {
            await notifications.NotifyUsersAsync([analystId], project.Id,
                $"{project.Code} · {project.Name} projesine iş analisti olarak atandınız.", cancellationToken);
        }

        if (project.ProjectManagerId is { } managerId && managerId != previousManagerId && managerId != actingUserId)
        {
            await notifications.NotifyUsersAsync([managerId], project.Id,
                $"{project.Code} · {project.Name} projesine proje yöneticisi olarak atandınız.", cancellationToken);
        }
    }

    private Expression<Func<Project, ProjectDto>> ToDto() =>
        p => new ProjectDto(
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            p.Source,
            p.RequestChannel,
            p.RequestType,
            p.CustomerName,
            p.RequesterName,
            p.RequiresNda,
            p.Status,
            p.CurrentStep != null
                ? new CurrentStepSummaryDto(
                    p.CurrentStep.Id, p.CurrentStep.Code,
                    p.CurrentStep.ProcessStage.Order, p.CurrentStep.ProcessStage.ShortTitle)
                : null,
            p.BusinessAnalyst != null ? new UserSummaryDto(p.BusinessAnalyst.Id, p.BusinessAnalyst.FullName) : null,
            p.ProjectManager != null ? new UserSummaryDto(p.ProjectManager.Id, p.ProjectManager.FullName) : null,
            p.Level,
            p.Priority,
            p.StartDate,
            p.DueDate,
            dbContext.Users
                .Where(u => u.Id == p.CreatedById)
                .Select(u => new UserSummaryDto(u.Id, u.FullName))
                .First(),
            p.CreatedAt,
            p.UpdatedAt);
}
