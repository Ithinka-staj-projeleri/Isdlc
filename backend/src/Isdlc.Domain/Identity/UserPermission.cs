using Isdlc.Domain.Common;

namespace Isdlc.Domain.Identity;

/// <summary>A permission given to a user by their company administrator. Unique per (UserId, Permission); not audited, like User.</summary>
public class UserPermission : ITenantOwned
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public Permission Permission { get; set; }
}
