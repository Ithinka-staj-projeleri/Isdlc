using System.Linq.Expressions;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.Risks.Dtos;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Risks;

public class RiskService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    INotificationService notifications) : IRiskService
{
    public async Task<PagedResult<RiskListItemDto>> ListAsync(
        int projectId, RiskFilter filter, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        if ((filter.Probability is null) != (filter.Impact is null))
        {
            throw new BusinessRuleException("Olasılık ve etki birlikte gönderilmelidir.");
        }

        var query = dbContext.Risks.AsNoTracking().Where(x => x.ProjectId == projectId);

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        if (filter.Category is { } category)
        {
            query = query.Where(x => x.Category == category);
        }

        if (filter.Severity is { } severity)
        {
            var (min, max) = RiskRules.ScoreRange(severity);
            query = query.Where(x => x.Probability * x.Impact >= min && x.Probability * x.Impact <= max);
        }

        if (filter is { Probability: { } probability, Impact: { } impact })
        {
            query = query.Where(x => x.Probability == probability && x.Impact == impact);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Status == RiskStatus.Closed ? 1 : 0)
            .ThenByDescending(x => x.Probability * x.Impact)
            .ThenBy(x => x.Code.Length)
            .ThenBy(x => x.Code)
            .ApplyPaging(pageRequest)
            .Select(x => new RiskListItemDto(
                x.Id,
                x.Code,
                x.Title,
                x.Category,
                x.Probability,
                x.Impact,
                x.Probability * x.Impact,
                x.Mitigation,
                new UserSummaryDto(x.OwnerUser.Id, x.OwnerUser.FullName),
                x.IsSharedWithRequester,
                x.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<RiskListItemDto>(items, totalCount);
    }

    public async Task<RiskMatrixDto> GetMatrixAsync(int projectId, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        var cells = await dbContext.Risks
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Status != RiskStatus.Closed)
            .GroupBy(x => new { x.Probability, x.Impact })
            .Select(g => new RiskMatrixCellDto(g.Key.Probability, g.Key.Impact, g.Count()))
            .ToListAsync(cancellationToken);

        // Highest probability first, like the rows of the heat map.
        cells = cells.OrderByDescending(c => c.Probability).ThenBy(c => c.Impact).ToList();

        int CountOf(RiskSeverity severity) => cells
            .Where(c => RiskRules.SeverityOf(RiskRules.Score(c.Probability, c.Impact)) == severity)
            .Sum(c => c.Count);

        return new RiskMatrixDto(
            cells,
            new RiskSeverityCountsDto(CountOf(RiskSeverity.Low), CountOf(RiskSeverity.Medium), CountOf(RiskSeverity.High)));
    }

    public async Task<RiskDto> GetAsync(int projectId, int riskId, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        return await dbContext.Risks
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Id == riskId)
            .Select(ToDto())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Risk bulunamadı.");
    }

    public async Task<RiskDto> CreateAsync(int projectId, CreateRiskDto request, CancellationToken cancellationToken)
    {
        var userId = RequireSignedIn();
        var project = await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        await ValidateOwnerAsync(request.OwnerUserId!.Value, cancellationToken);

        var number = await dbContext.Risks
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Code)
            .NextNumberAsync(cancellationToken);

        var risk = new Risk
        {
            ProjectId = projectId,
            Code = OpenIssueRules.FormatCode(RiskRules.CodePrefix, number),
            Status = RiskStatus.Open,
        };
        ApplyContent(risk, request);
        dbContext.Risks.Add(risk);

        await SaveNewAsync(risk, cancellationToken);
        await NotifyAsync(project, risk, previousOwnerUserId: null, previousScore: null, previousStatus: null, userId,
            cancellationToken);

        return await GetAsync(projectId, risk.Id, cancellationToken);
    }

    public async Task<RiskDto> UpdateAsync(
        int projectId, int riskId, UpdateRiskDto request, CancellationToken cancellationToken)
    {
        var userId = RequireSignedIn();
        var project = await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        var risk = await dbContext.Risks.SingleOrDefaultAsync(
                x => x.ProjectId == projectId && x.Id == riskId, cancellationToken)
            ?? throw new NotFoundException("Risk bulunamadı.");
        await ValidateOwnerAsync(request.OwnerUserId!.Value, cancellationToken);

        var previousOwnerUserId = risk.OwnerUserId;
        var previousScore = RiskRules.Score(risk.Probability, risk.Impact);
        var previousStatus = risk.Status;

        ApplyContent(risk, request);
        // [Required] on the DTO guarantees a value.
        risk.Status = request.Status!.Value;
        await dbContext.SaveChangesAsync(cancellationToken);

        await NotifyAsync(project, risk, previousOwnerUserId, previousScore, previousStatus, userId, cancellationToken);

        return await GetAsync(projectId, risk.Id, cancellationToken);
    }

    // ---- Helpers ----

    private int RequireSignedIn() =>
        currentUser.UserId ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

    private async Task ValidateOwnerAsync(int ownerUserId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == ownerUserId && u.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Seçilen sorumlu aktif bir kullanıcı değil.");
        }
    }

    private static void ApplyContent(Risk risk, CreateRiskDto request)
    {
        // [Required] and [Range] on the DTO guarantee these values.
        risk.Title = request.Title!.Trim();
        risk.Description = OpenIssueRules.Clean(request.Description);
        risk.Category = request.Category!.Value;
        risk.Probability = request.Probability!.Value;
        risk.Impact = request.Impact!.Value;
        risk.Mitigation = OpenIssueRules.Clean(request.Mitigation);
        risk.OwnerUserId = request.OwnerUserId!.Value;
        risk.IsSharedWithRequester = request.IsSharedWithRequester;
    }

    /// <summary>
    /// The owner hears that the risk was assigned to them; the project manager hears that it has just become high.
    /// Nobody is told about what they did themselves.
    /// </summary>
    private async Task NotifyAsync(
        ProjectRef project, Risk risk, int? previousOwnerUserId, int? previousScore, RiskStatus? previousStatus,
        int actingUserId, CancellationToken cancellationToken)
    {
        if (risk.OwnerUserId != previousOwnerUserId && risk.OwnerUserId != actingUserId)
        {
            await notifications.NotifyUsersAsync(
                [risk.OwnerUserId], project.Id, $"{project.Code} · {risk.Code} size atandı: {risk.Title}",
                cancellationToken);
        }

        var score = RiskRules.Score(risk.Probability, risk.Impact);
        if (!RiskRules.BecameHigh(previousScore, previousStatus, score, risk.Status))
        {
            return;
        }

        var projectManagerId = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == project.Id)
            .Select(p => p.ProjectManagerId)
            .SingleAsync(cancellationToken);

        if (projectManagerId is { } managerId && managerId != actingUserId)
        {
            await notifications.NotifyUsersAsync(
                [managerId], project.Id, $"{project.Code} · {risk.Code} yüksek risk (skor {score})", cancellationToken);
        }
    }

    /// <summary>
    /// Saves a new risk. Codes come from "highest + 1", so two risks saved at the same moment
    /// can get the same code; the unique index stops the second one and the user is asked to try again.
    /// </summary>
    private async Task SaveNewAsync(Risk risk, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var codeTaken = await dbContext.Risks
                .AsNoTracking()
                .AnyAsync(x => x.ProjectId == risk.ProjectId && x.Code == risk.Code, cancellationToken);

            if (codeTaken)
            {
                throw new ConflictException("Kayıt numarası çakıştı. Lütfen tekrar deneyin.");
            }

            throw;
        }
    }

    private Expression<Func<Risk, RiskDto>> ToDto() =>
        x => new RiskDto(
            x.Id,
            x.ProjectId,
            x.Code,
            x.Title,
            x.Description,
            x.Category,
            x.Probability,
            x.Impact,
            x.Probability * x.Impact,
            x.Mitigation,
            new UserSummaryDto(x.OwnerUser.Id, x.OwnerUser.FullName),
            x.IsSharedWithRequester,
            x.Status,
            dbContext.Users
                .Where(u => u.Id == x.CreatedById)
                .Select(u => new UserSummaryDto(u.Id, u.FullName))
                .First(),
            x.CreatedAt,
            x.UpdatedAt);
}
