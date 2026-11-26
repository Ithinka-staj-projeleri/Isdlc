using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;

namespace Isdlc.Application.Tenants.Dtos;

// Contract: docs/multi-tenancy.md → Uç noktalar, Platform Yöneticisi. No project, user or record detail (karar 10).

/// <summary>A company as the Platform Administrator sees it: only how many users it has.</summary>
public record TenantDto(int Id, string Code, string Name, bool IsActive, int UserCount, DateTime CreatedAt);

/// <summary>
/// The company's first administrator, opened with a temporary password. No title or permission: the account starts as
/// company administrator only and gives itself permissions if it also works in the flow (Faz 7 · A2, karar 4).
/// </summary>
public class TenantAdminDto
{
    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [MaxLength(100, ErrorMessage = "En fazla 100 karakter olabilir.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [MaxLength(256, ErrorMessage = "En fazla 256 karakter olabilir.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Geçici parola zorunludur.")]
    [MinLength(PasswordRules.MinLength, ErrorMessage = "Parola en az 12 karakter olmalıdır.")]
    [MaxLength(PasswordRules.MaxLength, ErrorMessage = "En fazla 200 karakter olabilir.")]
    public string TemporaryPassword { get; set; } = string.Empty;
}

/// <summary>POST /tenants.</summary>
public class CreateTenantDto
{
    [Required(ErrorMessage = "Şirket kodu zorunludur.")]
    [MaxLength(Tenant.CodeMaxLength, ErrorMessage = "Şirket kodu küçük harf, rakam ve tireden oluşmalıdır (2–50 karakter).")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şirket adı zorunludur.")]
    [MaxLength(Tenant.NameMaxLength, ErrorMessage = "En fazla 200 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şirketin ilk yöneticisi girilmelidir.")]
    public TenantAdminDto? Admin { get; set; }
}

/// <summary>PUT /tenants/{id}: the code never changes.</summary>
public class UpdateTenantDto
{
    [Required(ErrorMessage = "Şirket adı zorunludur.")]
    [MaxLength(Tenant.NameMaxLength, ErrorMessage = "En fazla 200 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

/// <summary>GET /tenants/current: the user's company, with its active administrators' names ("whom to ask", karar 2b).</summary>
public record CurrentTenantDto(int Id, string Code, string Name, IReadOnlyList<string> Admins);
