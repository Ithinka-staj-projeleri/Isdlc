using System.ComponentModel.DataAnnotations;

namespace Isdlc.Application.Tenants.Dtos;

/// <summary>
/// PUT /tenants/current/settings. Both switches are sent every time; a missing one is a validation error, not "off".
/// </summary>
public class UpdateTenantSettingsDto
{
    [Required(ErrorMessage = "Yapay zekâ ayarı belirtilmelidir.")]
    public bool? AiEnabled { get; set; }

    [Required(ErrorMessage = "Veri gönderme izni belirtilmelidir.")]
    public bool? AiDataSharingAllowed { get; set; }
}
