namespace Isdlc.Domain.Common;

/// <summary>
/// A record that belongs to one company (tenant). Every table has it except Tenants and Users (a user's TenantId may be
/// empty: the Platform Administrator). TenantId is filled by TenantInterceptor when the record is saved; never set it in
/// application code. Reads see only the signed-in user's company (global query filter). Rules: docs/multi-tenancy.md.
/// </summary>
public interface ITenantOwned
{
    int TenantId { get; set; }
}
