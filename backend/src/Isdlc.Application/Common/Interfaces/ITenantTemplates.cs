namespace Isdlc.Application.Common.Interfaces;

/// <summary>
/// The template seed (Seed/flows, Seed/document-types.json, Seed/question-sets) of the company in ITenantContext:
/// missing templates are copied to it, its copies the company has not changed (IsCustomized = false) follow the files.
/// Runs at startup for every active company and when a new company is opened (docs/multi-tenancy.md → Seed ve şablon).
/// Does not open a transaction and does not change ITenantContext; the caller does both.
/// </summary>
public interface ITenantTemplates
{
    /// <summary>InvalidOperationException when no company is set: templates always belong to one.</summary>
    Task ApplyAsync(CancellationToken cancellationToken = default);
}
