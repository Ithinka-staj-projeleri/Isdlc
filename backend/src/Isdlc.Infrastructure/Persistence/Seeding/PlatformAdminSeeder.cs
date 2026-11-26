using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Isdlc.Infrastructure.Persistence.Seeding;

/// <summary>
/// Opens the Platform Administrator once, after the companies are seeded, with no company set
/// (docs/multi-tenancy.md → Seed ve şablon, "Açılış sırası" step 4):
/// <list type="bullet">
/// <item>Development: platform@isdlc.local (or Platform:AdminEmail) with the test password (Identity:TestPassword).</item>
/// <item>Elsewhere: only when Platform:AdminEmail and Platform:AdminPassword are given; the password is temporary and
/// must be changed at the first sign-in.</item>
/// </list>
/// </summary>
public class PlatformAdminSeeder(
    IsdlcDbContext db,
    ITenantContext tenant,
    IPasswordHasher passwordHasher,
    PlatformAdminSettings settings,
    TimeProvider timeProvider,
    ILogger<PlatformAdminSeeder> logger)
{
    public const string DevelopmentEmail = "platform@isdlc.local";

    public async Task SeedAsync(bool isDevelopment, CancellationToken cancellationToken = default)
    {
        // tenant: the Platform Administrator has no company, so with no company set the filter finds nobody;
        // only whether one exists is read (docs/multi-tenancy.md → Okuma kuralı).
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.IsPlatformAdmin, cancellationToken))
        {
            return;
        }

        var email = settings.AdminEmail ?? (isDevelopment ? DevelopmentEmail : null);
        var password = isDevelopment ? settings.TestPassword : settings.AdminPassword;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No Platform Administrator: set Platform:AdminEmail and Platform:AdminPassword (or Identity:TestPassword in development).");
            return;
        }

        tenant.Use(null);
        db.Users.Add(new User
        {
            FullName = "Platform Yöneticisi",
            Email = email.Trim(),
            IsPlatformAdmin = true,
            PasswordHash = passwordHasher.Hash(password),
            MustChangePassword = !isDevelopment,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Opened the Platform Administrator {Email}.", email);
    }
}

/// <summary>Read from configuration when services are registered: Platform:AdminEmail, Platform:AdminPassword, Identity:TestPassword.</summary>
public record PlatformAdminSettings(string? AdminEmail, string? AdminPassword, string? TestPassword);
