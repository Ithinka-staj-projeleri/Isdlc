using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity.Dtos;

/// <summary>POST /auth/change-password (docs/multi-tenancy.md → Parola).</summary>
public class ChangePasswordDto
{
    [Required(ErrorMessage = "Mevcut parola zorunludur.")]
    [MaxLength(PasswordRules.MaxLength, ErrorMessage = "En fazla 200 karakter olabilir.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola zorunludur.")]
    [MinLength(PasswordRules.MinLength, ErrorMessage = "Parola en az 12 karakter olmalıdır.")]
    [MaxLength(PasswordRules.MaxLength, ErrorMessage = "En fazla 200 karakter olabilir.")]
    public string NewPassword { get; set; } = string.Empty;
}
