using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// Sign-in on a real database with the real password hasher. The JWT itself is made in the API project,
/// so a recording token service stands in for it here. Rule (AGENTS.md): every failed sign-in looks the same.
/// </summary>
[Collection("Database")]
public class AuthServiceTests(DatabaseFixture database)
{
    private const string Password = "Gizli-Parola-1";

    private sealed class RecordingTokenService : ITokenService
    {
        public List<User> CreatedFor { get; } = [];

        public TokenResult Create(User user)
        {
            CreatedFor.Add(user);
            return new TokenResult($"token-for-{user.Id}", new DateTime(2026, 11, 10, 18, 0, 0, DateTimeKind.Utc));
        }
    }

    private async Task<(LoginResultDto? Result, RecordingTokenService Tokens)> LoginAsync(string email, string password)
    {
        await using var scope = database.CreateScope();
        var tokens = new RecordingTokenService();
        var service = new AuthService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            tokens);
        var result = await service.LoginAsync(new LoginDto { Email = email, Password = password });
        return (result, tokens);
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsTokenAndUser()
    {
        var user = await TestUsers.CreateAsync(database, Password, role: UserRole.ProjectManager);

        var (result, tokens) = await LoginAsync(user.Email, Password);

        Assert.NotNull(result);
        Assert.Equal($"token-for-{user.Id}", result.Token);
        Assert.Equal(new DateTime(2026, 11, 10, 18, 0, 0, DateTimeKind.Utc), result.ExpiresAt);
        Assert.Equal(new AuthUserDto(user.Id, user.FullName, user.Email, UserRole.ProjectManager), result.User);
        Assert.Equal(user.Id, Assert.Single(tokens.CreatedFor).Id);
    }

    [Fact]
    public async Task Login_EmailWithSpacesAndCapitals_FindsTheUser()
    {
        var user = await TestUsers.CreateAsync(database, Password);

        var (result, _) = await LoginAsync($"  {user.Email.ToUpperInvariant()}  ", Password);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.User.Id);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNullAndMakesNoToken()
    {
        var user = await TestUsers.CreateAsync(database, Password);

        var (result, tokens) = await LoginAsync(user.Email, "Gizli-Parola-2");

        Assert.Null(result);
        Assert.Empty(tokens.CreatedFor);
    }

    [Fact]
    public async Task Login_PasswordWithOtherCase_ReturnsNull()
    {
        var user = await TestUsers.CreateAsync(database, Password);

        var (result, _) = await LoginAsync(user.Email, Password.ToUpperInvariant());

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsNull()
    {
        var (result, tokens) = await LoginAsync("kimse.yok@isdlc.local", Password);

        Assert.Null(result);
        Assert.Empty(tokens.CreatedFor);
    }

    [Fact]
    public async Task Login_InactiveUserWithCorrectPassword_ReturnsNull()
    {
        var user = await TestUsers.CreateAsync(database, Password, isActive: false);

        var (result, tokens) = await LoginAsync(user.Email, Password);

        Assert.Null(result);
        Assert.Empty(tokens.CreatedFor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("herhangi-bir-parola")]
    public async Task Login_UserWithoutPassword_ReturnsNull(string attempt)
    {
        // A user whose password was never set (empty hash) cannot sign in with anything, not even an empty password.
        var user = await TestUsers.CreateAsync(database, password: null);

        var (result, _) = await LoginAsync(user.Email, attempt);

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_SeedUserBeforeTestPasswordIsSet_ReturnsNull()
    {
        var (result, _) = await LoginAsync(TestProjects.Analyst, "");

        Assert.Null(result);
    }
}
