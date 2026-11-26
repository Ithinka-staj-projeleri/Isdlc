using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity.Dtos;

/// <summary>A person to pick in a form (business analyst, project manager, owner, ...), with their permissions.</summary>
public record UserLookupDto(int Id, string FullName, IReadOnlyList<Permission> Permissions);
