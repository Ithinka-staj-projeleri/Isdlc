using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Agents;

public class AiUsageService(
    IApplicationDbContext dbContext, ICurrentUserService currentUser, IAgentClient agents, TimeProvider timeProvider)
    : IAiUsageService
{
    public async Task<AiUsageSummaryDto> GetMonthAsync(string? month, CancellationToken cancellationToken)
    {
        var tenantId = EnsureTenantAdmin();
        var range = string.IsNullOrWhiteSpace(month)
            ? AiUsageRules.MonthOf(timeProvider.GetUtcNow().UtcDateTime)
            : AiUsageRules.ParseMonth(month.Trim()) ?? throw new BusinessRuleException(AiUsageRules.MonthFormatMessage);

        // The query filter keeps the rows to the administrator's company.
        var byAssistant = await dbContext.AiUsages
            .AsNoTracking()
            .Where(u => u.CreatedAt >= range.Start && u.CreatedAt < range.End)
            .GroupBy(u => u.Assistant)
            .Select(g => new
            {
                Assistant = g.Key,
                Requests = g.Count(),
                Failed = g.Count(u => !u.Succeeded),
                Input = g.Sum(u => (long)(u.InputTokens ?? 0)),
                Output = g.Sum(u => (long)(u.OutputTokens ?? 0)),
                Audio = g.Sum(u => (long)(u.AudioSeconds ?? 0)),
            })
            .ToListAsync(cancellationToken);

        // Tenants has no query filter; the company is always the administrator's own.
        var limit = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.AiMonthlyTokenLimit)
            .SingleOrDefaultAsync(cancellationToken);

        return new AiUsageSummaryDto(
            range.ToString(),
            byAssistant.Sum(a => a.Requests),
            byAssistant.Sum(a => a.Failed),
            byAssistant.Sum(a => a.Input),
            byAssistant.Sum(a => a.Output),
            byAssistant.Sum(a => a.Audio),
            limit,
            byAssistant
                .OrderBy(a => a.Assistant)
                .Select(a => new AiUsageByAssistantDto(a.Assistant, a.Requests, a.Input + a.Output))
                .ToList());
    }

    public async Task<AiModelsDto> ListModelsAsync(CancellationToken cancellationToken)
    {
        EnsureTenantAdmin();
        return new AiModelsDto(await agents.ListModelsAsync(cancellationToken));
    }

    /// <summary>The administrator's company id. A Platform Administrator has no company, so is refused like any non-admin.</summary>
    private int EnsureTenantAdmin()
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        if (!currentUser.IsTenantAdmin || currentUser.TenantId is not { } tenantId)
        {
            throw new ForbiddenException(TenantSettingsService.TenantAdminRequired);
        }

        return tenantId;
    }
}
