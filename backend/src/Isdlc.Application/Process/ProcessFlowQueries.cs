using Isdlc.Application.Common.Exceptions;
using Isdlc.Domain.Process;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Process;

/// <summary>Finding a company's flows and their steps as the services need them (Faz 7 · A7).</summary>
public static class ProcessFlowQueries
{
    /// <summary>
    /// The flow that starts the company's records of this use (ProcessFlow.UsedFor). 400 when the company has not chosen
    /// one: an administrator can take the use away from a flow while editing.
    /// </summary>
    public static async Task<int> FlowIdForAsync(
        this IQueryable<ProcessFlow> flows, ProcessFlowUse use, CancellationToken cancellationToken) =>
        await flows
            .Where(f => f.UsedFor == use)
            .Select(f => (int?)f.Id)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException(
            $"Şirkette {UseName(use)} için bir akış seçilmemiş. Şirket yöneticisi Süreç Kütüphanesi'nden seçebilir.");

    /// <summary>The flow's active steps with their order and rules: what the rules walk through (inactive steps never come).</summary>
    public static async Task<IReadOnlyList<ConditionalStep>> ConditionalStepsAsync(
        this IQueryable<ProcessStep> steps, int flowId, CancellationToken cancellationToken) =>
        await steps
            .AsNoTracking()
            .Where(s => s.ProcessFlowId == flowId && s.IsActive)
            .OrderBy(s => s.Order)
            .Select(s => new ConditionalStep(s.Id, s.Order, s.Conditions))
            .ToListAsync(cancellationToken);

    public static string UseName(ProcessFlowUse use) => use switch
    {
        ProcessFlowUse.NewRequest => "yeni talepler",
        ProcessFlowUse.AdditionalModule => "ek geliştirmeler",
        ProcessFlowUse.Improvement => "süreç iyileştirme önerileri",
        ProcessFlowUse.ResourcePlanning => "kaynak planlama",
        ProcessFlowUse.Billing => "faturalama",
        _ => use.ToString(),
    };
}
