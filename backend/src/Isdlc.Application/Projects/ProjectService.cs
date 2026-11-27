using System.Linq.Expressions;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
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

    /// <summary>
    /// Projects an add-on can be opened on: new software projects that have been accepted (active in a project stage)
    /// or are closed. A waiting request, a rejected, lost or redirected one, and an add-on itself cannot have add-ons.
    /// </summary>
    private static readonly Expression<Func<Project, bool>> AcceptsAddOns = p =>
        // A request flow's own record, not an add-on: add-ons cannot have add-ons (Faz 7 · A7: by kind, not by flow code).
        p.ParentProjectId == null && p.ProcessFlow.Kind == ProcessFlowKind.Request
        && (p.Status == ProjectStatus.Closed
            || (p.Status == ProjectStatus.Active
                && p.CurrentStep != null
                && p.ProcessFlow.ProjectStageOrder != null
                && p.CurrentStep.ProcessStage.Order >= p.ProcessFlow.ProjectStageOrder));

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
            // A postponed request waits on its step, so it stays among the requests ("Ertelendi").
            query = query.Where(p =>
                (p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Postponed)
                && p.CurrentStep != null
                && (p.ProcessFlow.ProjectStageOrder == null
                    || p.CurrentStep.ProcessStage.Order < p.ProcessFlow.ProjectStageOrder));
        }
        else if (filter.Phase == ProjectPhase.Project)
        {
            query = query.Where(p =>
                (p.Status != ProjectStatus.Active && p.Status != ProjectStatus.Postponed)
                || (p.CurrentStep != null
                    && p.ProcessFlow.ProjectStageOrder != null
                    && p.CurrentStep.ProcessStage.Order >= p.ProcessFlow.ProjectStageOrder));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Proje No must match exactly ("14" does not find 114) and also finds the project's add-ons ("14 / EG-01");
            // the database collation ignores case, so "portal" also finds "Portal".
            var search = filter.Search.Trim();
            query = query.Where(p =>
                p.Code == search
                || (p.ParentProject != null && p.ParentProject.Code == search)
                || p.Name.Contains(search)
                || p.CustomerName.Contains(search));
        }

        if (filter.BusinessAnalystId is { } analystId)
        {
            query = query.Where(p => p.BusinessAnalystId == analystId);
        }

        if (filter.ParentProjectId is { } parentId)
        {
            query = query.Where(p => p.ParentProjectId == parentId);
        }

        if (filter.AcceptsAddOns == true)
        {
            query = query.Where(AcceptsAddOns);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // Newest project first, by Proje No; an add-on sits with its project ("14 / EG-02", "14 / EG-01", "14").
            // Longer numbers first, so "1000" comes before "999".
            .OrderByDescending(p => (p.ParentProject != null ? p.ParentProject.Code : p.Code).Length)
            .ThenByDescending(p => p.ParentProject != null ? p.ParentProject.Code : p.Code)
            .ThenByDescending(p => p.Code.Length)
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

        // Opening a request is the flow's first step, so it needs the analysis permission (docs/configurability.md).
        if (!await dbContext.IsActiveUserWithAsync(userId, Permission.Analysis, cancellationToken))
        {
            throw new ForbiddenException(
                PermissionMessages.ForNewRequest(await dbContext.TenantAdminNamesAsync(cancellationToken)));
        }

        await ValidatePeopleAsync(request.BusinessAnalystId, request.ProjectManagerId, cancellationToken);

        // An add-on (ek geliştirme) belongs to a project and follows the add-on flow; every other request is a new project.
        var parent = request.ParentProjectId is { } parentId ? await FindAddOnParentAsync(parentId, cancellationToken) : null;
        var use = parent is null ? ProcessFlowUse.NewRequest : ProcessFlowUse.AdditionalModule;

        // Agreed with Process: a new request starts Active at the first step of its flow whose conditions hold.
        // The flow is the one the company starts these records with (Faz 7 · A7).
        var flowId = await dbContext.ProcessFlows.FlowIdForAsync(use, cancellationToken);
        var flowSteps = await dbContext.ProcessSteps.ConditionalStepsAsync(flowId, cancellationToken);

        var project = new Project { Status = ProjectStatus.Active, ProcessFlowId = flowId, ParentProjectId = parent?.Id };
        ApplyContent(project, request);
        await ApplyCustomValuesAsync(project, request, cancellationToken);
        project.CurrentStepId = StepConditions.FirstStepId(
                flowSteps, new ConditionContext(project.Source, project.RequestType, project.RequiresOffer, project.Level,
                    CustomValueRules.ToText(project.CustomValues),
                    await dbContext.CustomFields.ActiveKeysAsync(cancellationToken)))
            ?? throw new BusinessRuleException("Akışta bu talep için yapılacak bir adım yok.");
        dbContext.Projects.Add(project);

        await SaveWithNewCodeAsync(project, parent is null
            ? async () => ProjectCodes.Format(ProjectCodes.NextNumber(await HighestCodeAsync(flowId, cancellationToken)))
            : async () => ProjectCodes.FormatAddOn(
                parent.Code, ProjectCodes.NextAddOnNumber(await HighestAddOnCodeAsync(parent.Id, cancellationToken))),
            cancellationToken);
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

        // The record stays what it was opened as: a new project or an add-on of the same project.
        if (request.ParentProjectId != project.ParentProjectId)
        {
            throw new BusinessRuleException(project.ParentProjectId is null
                ? "Yeni yazılım projesi sonradan ek geliştirmeye çevrilemez."
                : "Ek geliştirmenin ait olduğu proje değiştirilemez.");
        }

        await ValidatePeopleAsync(request.BusinessAnalystId, request.ProjectManagerId, cancellationToken);

        if (project.Source != request.Source || project.RequiresOffer != request.RequiresOffer)
        {
            await EnsureStepConditionsCanChangeAsync(project, cancellationToken);
        }

        var previousAnalystId = project.BusinessAnalystId;
        var previousManagerId = project.ProjectManagerId;
        ApplyContent(project, request);
        await ApplyCustomValuesAsync(project, request, cancellationToken);

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

    /// <summary>
    /// Checks the company's own fields against its active field definitions and keeps the new values (Faz 7 · A6);
    /// 400 with "customValues.{key}" errors. Values of inactive fields stay as they were.
    /// </summary>
    private async Task ApplyCustomValuesAsync(
        Project project, CreateProjectDto request, CancellationToken cancellationToken)
    {
        var fields = await dbContext.CustomFields.ActiveDefinitionsAsync(cancellationToken);
        var result = CustomValueRules.Apply(fields, project.CustomValues, request.CustomValues);
        if (!result.IsValid)
        {
            throw new FieldValidationException(result.Errors);
        }

        project.CustomValues = result.Json;
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Source and RequiresOffer decide which steps are skipped. Once the record has reached the first step that
    /// depends on them, changing them would make the history inconsistent (409). Decision D1-3, docs/projects-api.md.
    /// </summary>
    private async Task EnsureStepConditionsCanChangeAsync(Project project, CancellationToken cancellationToken)
    {
        // The rules are a JSON column: the flow's steps (a few dozen) are read and checked here.
        var flowSteps = await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.ProcessFlowId == project.ProcessFlowId && s.IsActive)
            .OrderBy(s => s.Order)
            .Select(s => new { s.Order, s.Code, s.Conditions })
            .ToListAsync(cancellationToken);
        var firstDependentStep = flowSteps.FirstOrDefault(s =>
            StepConditions.DependsOn(s.Conditions, StepConditionRule.Source)
            || StepConditions.DependsOn(s.Conditions, StepConditionRule.RequiresOffer));
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

    private sealed record AddOnParent(int Id, string Code);

    /// <summary>The project an add-on is opened on; 400 when it does not exist or cannot have add-ons.</summary>
    private async Task<AddOnParent> FindAddOnParentAsync(int parentId, CancellationToken cancellationToken)
    {
        var parent = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == parentId)
            .Select(p => new { p.Id, p.Code, p.ParentProjectId, FlowKind = p.ProcessFlow.Kind })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Ek geliştirmenin ait olduğu proje bulunamadı.");

        if (parent.ParentProjectId is not null || parent.FlowKind != ProcessFlowKind.Request)
        {
            throw new BusinessRuleException("Ek geliştirme yalnızca bir yeni yazılım projesine açılabilir.");
        }

        if (!await dbContext.Projects.Where(p => p.Id == parentId).AnyAsync(AcceptsAddOns, cancellationToken))
        {
            throw new BusinessRuleException(
                "Ek geliştirme yalnızca kabul edilmiş (Projeler'deki) ya da tamamlanmış bir projeye açılabilir.");
        }

        return new AddOnParent(parent.Id, parent.Code);
    }

    /// <summary>
    /// An assigned person must be an active user with the matching permission (400): the analyst Analysis, the project
    /// manager ProjectManagement. Another company's user is not found, so they fail the same way.
    /// </summary>
    private async Task ValidatePeopleAsync(
        int? businessAnalystId, int? projectManagerId, CancellationToken cancellationToken)
    {
        if (businessAnalystId is { } analystId
            && !await dbContext.IsActiveUserWithAsync(analystId, Permission.Analysis, cancellationToken))
        {
            throw new BusinessRuleException(PermissionMessages.SelectedUserLacks(Permission.Analysis));
        }

        if (projectManagerId is { } managerId
            && !await dbContext.IsActiveUserWithAsync(managerId, Permission.ProjectManagement, cancellationToken))
        {
            throw new BusinessRuleException(PermissionMessages.SelectedUserLacks(Permission.ProjectManagement));
        }
    }

    /// <summary>
    /// Gives the record the next code and saves it. Codes come from "highest + 1", so two requests saved at the
    /// same moment can pick the same code; the unique index stops the second one, which then tries again with a
    /// new number. The user is never asked to resend the form.
    /// </summary>
    private async Task SaveWithNewCodeAsync(
        Project project, Func<Task<string>> nextCode, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            project.Code = await nextCode();

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

    /// <summary>The highest add-on code of the project. Longer codes first, so "EG-100" comes before "EG-99".</summary>
    private Task<string?> HighestAddOnCodeAsync(int parentId, CancellationToken cancellationToken) =>
        dbContext.Projects
            .AsNoTracking()
            .Where(p => p.ParentProjectId == parentId)
            .OrderByDescending(p => p.Code.Length)
            .ThenByDescending(p => p.Code)
            .Select(p => p.Code)
            .FirstOrDefaultAsync(cancellationToken);

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
            p.ParentProject != null
                ? new ParentProjectDto(p.ParentProject.Id, p.ParentProject.Code, p.ParentProject.Name)
                : null,
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
            p.UpdatedAt,
            CustomValueRules.Read(p.CustomValues));
}
