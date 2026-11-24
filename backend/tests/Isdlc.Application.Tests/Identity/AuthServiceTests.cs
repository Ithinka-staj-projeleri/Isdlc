using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Isdlc.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
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

    private async Task<TenantSummaryDto> IthinkaAsync()
    {
        await using var scope = database.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IsdlcDbContext>().Tenants
            .SingleAsync(t => t.Id == Tenant.IthinkaId);
        return new TenantSummaryDto(tenant.Id, tenant.Code, tenant.Name);
    }

    private async Task<LoginResultDto> ChangeAsync(int userId, string current, string next)
    {
        await using var scope = database.CreateScope();
        var service = new AuthService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            new RecordingTokenService());
        return await service.ChangePasswordAsync(userId, new ChangePasswordDto { CurrentPassword = current, NewPassword = next });
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
        Assert.Equal(
            new AuthUserDto(user.Id, user.FullName, user.Email, UserRole.ProjectManager, await IthinkaAsync(), IsTenantAdmin: false),
            result.User);
        Assert.False(result.MustChangePassword);
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

    // ---- Companies (Faz 6 · T2, docs/multi-tenancy.md → Giriş) ----

    private async Task<int> CreateTenantAsync(bool isActive)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var tenant = new Tenant
        {
            Code = $"deneme-{Guid.NewGuid():N}"[..20], Name = "Deneme A.Ş.", IsActive = isActive, CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    [Fact]
    public async Task Login_UserOfAnotherCompany_IsFoundAndTheTokenCarriesTheCompany()
    {
        // Sign-in runs before any company is known (here: in an Ithinka scope) and still finds the user.
        var tenantId = await CreateTenantAsync(isActive: true);
        var user = await TestUsers.CreateAsync(database, Password, tenantId: tenantId);

        var (result, tokens) = await LoginAsync(user.Email, Password);

        Assert.NotNull(result);
        Assert.Equal(tenantId, Assert.Single(tokens.CreatedFor).TenantId);
    }

    [Fact]
    public async Task Login_UserOfAnInactiveCompany_ReturnsNullLikeAWrongPassword()
    {
        var tenantId = await CreateTenantAsync(isActive: false);
        var user = await TestUsers.CreateAsync(database, Password, tenantId: tenantId);

        var (result, tokens) = await LoginAsync(user.Email, Password);

        Assert.Null(result);
        Assert.Empty(tokens.CreatedFor);
    }

    // ---- Administrators and temporary passwords (Faz 6 · T4, docs/multi-tenancy.md → Kimlik, Parola) ----

    [Fact]
    public async Task Login_TenantAdminWithTemporaryPassword_SaysSoInTheAnswer()
    {
        var user = await TestUsers.CreateAsync(database, Password, isTenantAdmin: true, mustChangePassword: true);

        var (result, tokens) = await LoginAsync(user.Email, Password);

        Assert.NotNull(result);
        Assert.True(result.User.IsTenantAdmin);
        Assert.True(result.MustChangePassword);
        // The token is made from the same user, so it carries the same marks.
        var tokenUser = Assert.Single(tokens.CreatedFor);
        Assert.True(tokenUser.IsTenantAdmin && tokenUser.MustChangePassword);
    }

    [Fact]
    public async Task Login_PlatformAdmin_HasNoCompany()
    {
        var admin = await TestUsers.CreateAsync(database, Password, role: UserRole.PlatformAdmin);

        var (result, tokens) = await LoginAsync(admin.Email, Password);

        Assert.NotNull(result);
        Assert.Null(admin.TenantId);
        Assert.Null(result.User.Tenant);
        Assert.Equal(UserRole.PlatformAdmin, result.User.Role);
        Assert.Null(Assert.Single(tokens.CreatedFor).TenantId);
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrent_SavesTheNewOneAndClearsTheMark()
    {
        var user = await TestUsers.CreateAsync(database, Password, mustChangePassword: true);
        const string next = "Yeni-Parola-2026!";

        var result = await ChangeAsync(user.Id, Password, next);

        Assert.False(result.MustChangePassword);
        Assert.Null((await LoginAsync(user.Email, Password)).Result);
        var signedIn = (await LoginAsync(user.Email, next)).Result;
        Assert.NotNull(signedIn);
        Assert.False(signedIn.MustChangePassword);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_IsRefusedAndKeepsTheOldOne()
    {
        var user = await TestUsers.CreateAsync(database, Password, mustChangePassword: true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ChangeAsync(user.Id, "Yanlis-Parola-1", "Yeni-Parola-2026!"));

        Assert.Equal("Mevcut parola hatalı.", error.Message);
        Assert.True((await LoginAsync(user.Email, Password)).Result!.MustChangePassword);
    }

    [Fact]
    public async Task ChangePassword_SameAsTheOldOne_IsRefused()
    {
        var user = await TestUsers.CreateAsync(database, Password);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ChangeAsync(user.Id, Password, Password));

        Assert.Equal("Yeni parola eskisinden farklı olmalıdır.", error.Message);
    }

    [Fact]
    public async Task ChangePassword_PlatformAdmin_FindsTheirOwnRowWithoutACompany()
    {
        var admin = await TestUsers.CreateAsync(database, Password, role: UserRole.PlatformAdmin, mustChangePassword: true);

        var result = await ChangeAsync(admin.Id, Password, "Platform-Parola-2026");

        Assert.False(result.MustChangePassword);
        Assert.Null(result.User.Tenant);
    }

    [Fact]
    public async Task ChangePassword_InactiveUser_IsForbidden()
    {
        var user = await TestUsers.CreateAsync(database, Password, isActive: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => ChangeAsync(user.Id, Password, "Yeni-Parola-2026!"));
    }
}
