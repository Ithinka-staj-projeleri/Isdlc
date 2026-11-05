using Isdlc.Application.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Isdlc.Infrastructure.Persistence.Seeding;

/// <summary>
/// Development only: gives seed users without a password the test password from configuration
/// ("Identity:TestPassword", set with dotnet user-secrets). No password is stored in the repository.
/// </summary>
public class UserPasswordSeeder(
    IsdlcDbContext db, IPasswordHasher passwordHasher, IConfiguration configuration, ILogger<UserPasswordSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var password = configuration["Identity:TestPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Identity:TestPassword is not set; seed users cannot sign in.");
            return;
        }

        var users = await db.Users.Where(u => u.PasswordHash == "").ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            user.PasswordHash = passwordHasher.Hash(password);
        }

        if (users.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Set the test password for {Users} users.", users.Count);
        }
    }
}