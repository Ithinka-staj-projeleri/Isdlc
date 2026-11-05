using Isdlc.Application.Common.Interfaces;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Infrastructure.Identity;

/// <summary>
/// TEMPORARY: until the Identity module adds sign-in, every request runs as one fixed seed user.
/// This class and its registration in DependencyInjection.cs are the only places to change.
/// The user can be switched for local testing with the "TemporaryUser:Email" setting
/// (appsettings.Development.json or the TemporaryUser__Email environment variable).
/// </summary>
public class TemporaryCurrentUserService(string email) : ICurrentUserService
{
    public const string DefaultEmail = "elif.yilmaz@isdlc.local";

    public string Email { get; } = email;

    public int? UserId { get; private set; }

    /// <summary>Looks up the user's Id once at startup, after seeding.</summary>
    public async Task InitializeAsync(IsdlcDbContext dbContext, CancellationToken cancellationToken = default)
    {
        UserId = await dbContext.Users
            .Where(u => u.Email == Email)
            .Select(u => (int?)u.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Temporary user '{Email}' was not found. Is the seed loaded?");
    }
}
