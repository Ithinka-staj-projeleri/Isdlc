using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Agents;

namespace Isdlc.Application.Tenants.Dtos;

/// <summary>
/// PUT /tenants/current/settings. Both switches are sent every time; a missing one is a validation error, not "off".
/// The model and the limit are sent every time too; empty means the service default / no limit (Faz 7 · A9).
/// </summary>
public class UpdateTenantSettingsDto
{
    [Required(ErrorMessage = "Yapay zekâ ayarı belirtilmelidir.")]
    public bool? AiEnabled { get; set; }

    [Required(ErrorMessage = "Veri gönderme izni belirtilmelidir.")]
    public bool? AiDataSharingAllowed { get; set; }

    /// <summary>One of GET /agents/models; checked by the service (400 "Seçilen model kullanılamıyor.").</summary>
    [MaxLength(AiUsage.MaxModelLength, ErrorMessage = "Model adı en fazla 100 karakter olabilir.")]
    public string? AiModel { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Aylık sınır 1 ya da daha büyük bir sayı olmalıdır.")]
    public int? AiMonthlyTokenLimit { get; set; }
}
