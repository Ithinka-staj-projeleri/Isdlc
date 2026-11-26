using Isdlc.Application.Common;
using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity.Dtos;

/// <summary>GET /api/v1/permissions: the permission list and the ready-made sets, so the screen keeps no list of its own.</summary>
public record PermissionCatalogDto(IReadOnlyList<PermissionDto> Permissions, IReadOnlyList<PermissionPresetDto> Presets)
{
    public static PermissionCatalogDto Build() => new(
        Enum.GetValues<Permission>().Select(p => new PermissionDto(p, p.ToDisplayName())).ToList(),
        PermissionPresets.All.Select(p => new PermissionPresetDto(p.Name, p.Permissions)).ToList());
}

public record PermissionDto(Permission Value, string Name);

public record PermissionPresetDto(string Name, IReadOnlyList<Permission> Permissions);
