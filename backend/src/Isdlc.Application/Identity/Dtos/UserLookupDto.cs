using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity.Dtos;

/// <summary>A person to pick in a form (business analyst, project manager, owner, ...).</summary>
public record UserLookupDto(int Id, string FullName, UserRole Role);
