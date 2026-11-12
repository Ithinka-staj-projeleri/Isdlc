using System.Linq.Expressions;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications;
using Isdlc.Application.Process;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Projects;

public class ProjectService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    INotificationService notifications) : IProjectService
{
    /// <summary>The first try plus 3 retries when another request took the same Proje No at the same moment.</summary>
    private const int MaxCodeAttempts = 4;

    public async Task<PagedResult<ProjectListItemDto>> ListAsync(
        ProjectFilter filter, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        // Talepler and Projeler list only request flows; improvement, planning and billing records have their own menus.
        var query = dbContext.Projects.AsNoTracking().Where(p => p.ProcessFlow.Kind == ProcessFlowKind.Request);

        if (filter.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        // The two phases split every record in two: a waiting request (active, before the flow's project stage) or not.
        // A closed record is never a waiting request, even if it was closed early (e.g. rejected at step 4).
        // A flow without a project stage never turns its records into projects (docs/process-api.md → "Talep mi, proje mi").
        if (filter.Phase == ProjectPhase.Request)
        {
            query = query.Where(p =>
                p.Status == ProjectStatus.Active
                && p.CurrentStep != null
                && (p.ProcessFlow.ProjectStageOrder == null
                    || p.CurrentStep.ProcessStage.Order < p.ProcessFlow.ProjectStageOrder));
        }
        else if (filter.Phase == ProjectPhase.Project)
        {
            query = query.Where(p =>
                p.Status != ProjectStatus.Active
                || (p.CurrentStep != null
                    && p.ProcessFlow.ProjectStageOrder != null
                    && p.CurrentStep.ProcessStage.Order >= p.ProcessFlow.ProjectStageOrder));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Proje No must match exactly ("14" does not find 114); the database collation ignores case, so "portal"
            // also finds "Portal".
            var search = filter.Search.Trim();
            query = query.Where(p =>
                p.Code == search || p.Name.Contains(search) || p.CustomerName.Contains(search));
        }

        if (filter.BusinessAnalystId is { } analystId)
        {
            query = query.Where(p => p.BusinessAnalystId == analystId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // Newest request first. Longer numbers first, so "1000" comes before "999".
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
                p.RequestDate,
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

        // Agreed with Process: a new request starts Active at the first step of its flow whose conditions hold.
        var flowId = await dbContext.ProcessFlows
            .Where(f => f.Code == ProcessFlowCodes.NewRequest)
            .Select(f => f.Id)
            .SingleAsync(cancellationToken);
        var flowSteps = await dbContext.ProcessSteps
            .Where(s => s.ProcessFlowId == flowId)
            .Select(s => new ConditionalStep(
                s.Id, s.Order, new StepCondition(s.RequiresExternalRequest, s.RequiresOffer, s.MinLevel)))
            .ToListAsync(cancellationToken);

        var project = new Project { Status = ProjectStatus.Active, ProcessFlowId = flowId };
        ApplyContent(project, request);
        project.CurrentStepId = StepConditions.FirstStepId(
                flowSteps, new ConditionContext(project.Source, project.RequiresOffer, project.Level))
            ?? throw new InvalidOperationException($"Flow {ProcessFlowCodes.NewRequest} has no step for this request.");
        dbContext.Projects.Add(project);

        await SaveWithNewCodeAsync(project, flowId, cancellationToken);
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

        if (project.Source != request.Source || project.RequiresOffer != request.RequiresOffer)
        {
            await EnsureStepConditionsCanChangeAsync(project, cancellationToken);
        }

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
        project.RequestChannelOther = project.RequestChannel == RequestChannel.Other
            ? TrimToNull(request.RequestChannelOther)
            : null;
        project.RequestType = request.RequestType!.Value;
        project.CustomerName = request.CustomerName!.Trim();
        project.RequesterName = request.RequesterName!.Trim();
        project.RequesterContact = TrimToNull(request.RequesterContact);
        project.Notes = TrimToNull(request.Notes);
        project.RequiresOffer = request.RequiresOffer!.Value;
        project.RequiresNda = request.RequiresNda;
        project.BusinessAnalystId = request.BusinessAnalystId;
        project.ProjectManagerId = request.ProjectManagerId;
        project.RequestDate = request.RequestDate!.Value;
        project.DueDate = request.DueDate;
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Source and RequiresOffer decide which steps are skipped. Once the record has reached the first step that
    /// depends on them, changing them would make the history inconsistent (409). Decision D1-3, docs/projects-api.md.
    /// </summary>
    private async Task EnsureStepConditionsCanChangeAsync(Project project, CancellationToken cancellationToken)
    {
        var firstDependentStep = await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.ProcessFlowId == project.ProcessFlowId && (s.RequiresExternalRequest || s.RequiresOffer))
            .OrderBy(s => s.Order)
            .Select(s => new { s.Order, s.Code })
            .FirstOrDefaultAsync(cancellationToken);
        if (firstDependentStep is null)
        {
            return;
        }

        var currentOrder = await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.Id == project.CurrentStepId)
            .Select(s => (int?)s.Order)
            .SingleOrDefaultAsync(cancellationToken);
        if (currentOrder >= firstDependentStep.Order)
        {
            throw new ConflictException(
                $"Talep kaynağı ve teklif bilgisi {firstDependentStep.Code}. adıma gelindikten sonra değiştirilemez.");
        }
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
    /// Gives the project the next Proje No and saves it. Numbers come from "highest + 1", so two requests
    /// saved at the same moment can pick the same number; the unique index stops the second one, which then
    /// tries again with a new number. The user is never asked to resend the form.
    /// </summary>
    private async Task SaveWithNewCodeAsync(Project project, int flowId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            project.Code = ProjectCodes.Format(ProjectCodes.NextNumber(await HighestCodeAsync(flowId, cancellationToken)));

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

    /// <summary>
    /// Highest Proje No among the records of the flow; other flows number their records their own way.
    /// Longer numbers first, so "1000" comes before "999".
    /// </summary>
    private Task<string?> HighestCodeAsync(int flowId, CancellationToken cancellationToken)
    {
        return dbContext.Projects
            .AsNoTracking()
            // Only plain numbers count; a record with any other code (e.g. written by hand) is ignored.
            .Where(p => p.ProcessFlowId == flowId && !EF.Functions.Like(p.Code, "%[^0-9]%"))
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
            p.RequestChannelOther,
            p.RequestType,
            p.CustomerName,
            p.RequesterName,
            p.RequesterContact,
            p.Notes,
            p.RequiresOffer,
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
            p.RequestDate,
            p.DueDate,
            dbContext.Users
                .Where(u => u.Id == p.CreatedById)
                .Select(u => new UserSummaryDto(u.Id, u.FullName))
                .First(),
            p.CreatedAt,
            p.UpdatedAt);
}
