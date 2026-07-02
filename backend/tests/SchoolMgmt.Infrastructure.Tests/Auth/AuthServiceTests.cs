using Microsoft.Extensions.Options;
using SchoolMgmt.Application.Auth;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Infrastructure.Tests.Auth.Fakes;
using SchoolMgmt.Infrastructure.Tests.Fakes;

namespace SchoolMgmt.Infrastructure.Tests.Auth;

public class AuthServiceTests
{
    private static (AuthService Service, FakeUserRepository Users, FakeRefreshTokenRepository RefreshTokens, FakeDateTimeProvider Clock)
        CreateService(DateTimeOffset now)
    {
        var users = new FakeUserRepository();
        var refreshTokens = new FakeRefreshTokenRepository(users);
        var clock = new FakeDateTimeProvider(now);
        var options = Options.Create(new JwtOptions { AccessTokenMinutes = 15, RefreshTokenDays = 30, SessionRefreshTokenHours = 24 });

        var service = new AuthService(
            users,
            refreshTokens,
            new FakeUnitOfWork(),
            new FakePasswordHasher(),
            new FakeJwtTokenGenerator(),
            clock,
            options);

        return (service, users, refreshTokens, clock);
    }

    private static User SeedUser(FakeUserRepository users, string email = "admin@demoschool.test", string password = "correct-password")
    {
        var user = new User
        {
            Email = email,
            PasswordHash = new FakePasswordHasher().HashPassword(password),
            DisplayName = "Demo Admin",
            Role = UserRole.Admin,
            SchoolId = Guid.NewGuid(),
        };
        users.Seed(user);
        return user;
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTokenPair()
    {
        var (service, users, _, _) = CreateService(DateTimeOffset.UtcNow);
        var user = SeedUser(users);

        var result = await service.LoginAsync(new LoginRequest(user.Email, "correct-password"));

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.User.Id);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        var (service, users, _, _) = CreateService(DateTimeOffset.UtcNow);
        var user = SeedUser(users);

        var result = await service.LoginAsync(new LoginRequest(user.Email, "wrong-password"));

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithNonexistentEmail_ReturnsNull_SameAsWrongPassword()
    {
        var (service, _, _, _) = CreateService(DateTimeOffset.UtcNow);

        var result = await service.LoginAsync(new LoginRequest("nobody@demoschool.test", "anything"));

        Assert.Null(result); // identical failure shape to wrong-password — no user enumeration
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_RotatesAndReturnsNewPair_UnderSameSessionId()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, users, refreshTokens, _) = CreateService(now);
        var user = SeedUser(users);
        var login = await service.LoginAsync(new LoginRequest(user.Email, "correct-password"));
        var originalSessionId = refreshTokens.All.Single().SessionId;

        var refreshed = await service.RefreshAsync(login!.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken); // rotated — new raw token
        Assert.Equal(2, refreshTokens.All.Count); // original + new
        var newToken = refreshTokens.All.Single(t => t.RevokedAt == null);
        Assert.Equal(originalSessionId, newToken.SessionId); // same family
        var oldToken = refreshTokens.All.Single(t => t.Id != newToken.Id);
        Assert.NotNull(oldToken.RevokedAt);
        Assert.Equal(newToken.Id, oldToken.ReplacedByTokenId);
    }

    [Fact]
    public async Task RefreshAsync_ReusingAlreadyRotatedToken_RevokesEntireSessionFamily()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, users, refreshTokens, _) = CreateService(now);
        var user = SeedUser(users);
        var login = await service.LoginAsync(new LoginRequest(user.Email, "correct-password"));
        var refreshed = await service.RefreshAsync(login!.RefreshToken); // legitimate rotation

        // Replay the OLD (already-rotated) token — theft signal.
        var reuseResult = await service.RefreshAsync(login.RefreshToken);

        Assert.Null(reuseResult);
        Assert.All(refreshTokens.All, t => Assert.NotNull(t.RevokedAt)); // whole family revoked, incl. the legitimately-rotated one

        // The newest (legitimately rotated) token must now also be rejected.
        var attemptWithNewest = await service.RefreshAsync(refreshed!.RefreshToken);
        Assert.Null(attemptWithNewest);
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredToken_RejectsAndRevokesIt()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, users, refreshTokens, clock) = CreateService(now);
        var user = SeedUser(users);
        var login = await service.LoginAsync(new LoginRequest(user.Email, "correct-password"));

        clock.UtcNow = now.AddHours(25); // past the 24-hour SessionRefreshTokenHours lifetime (RememberMe defaults to false)

        var result = await service.RefreshAsync(login!.RefreshToken);

        Assert.Null(result);
        Assert.NotNull(refreshTokens.All.Single().RevokedAt);
    }

    [Fact]
    public async Task LoginAsync_WithRememberMeFalse_UsesSessionHoursLifetime()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, users, refreshTokens, _) = CreateService(now);
        var user = SeedUser(users);

        var result = await service.LoginAsync(new LoginRequest(user.Email, "correct-password", RememberMe: false));

        Assert.NotNull(result);
        Assert.False(result!.RememberMe);
        var token = refreshTokens.All.Single();
        Assert.False(token.RememberMe);
        Assert.Equal(now.AddHours(24), token.ExpiresAt);
    }

    [Fact]
    public async Task LoginAsync_WithRememberMeTrue_UsesRefreshTokenDaysLifetime()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, users, refreshTokens, _) = CreateService(now);
        var user = SeedUser(users);

        var result = await service.LoginAsync(new LoginRequest(user.Email, "correct-password", RememberMe: true));

        Assert.NotNull(result);
        Assert.True(result!.RememberMe);
        var token = refreshTokens.All.Single();
        Assert.True(token.RememberMe);
        Assert.Equal(now.AddDays(30), token.ExpiresAt);
    }

    [Fact]
    public async Task RefreshAsync_PreservesRememberMeFromOriginalToken()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, users, refreshTokens, _) = CreateService(now);
        var user = SeedUser(users);
        var login = await service.LoginAsync(new LoginRequest(user.Email, "correct-password", RememberMe: true));

        var refreshed = await service.RefreshAsync(login!.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.True(refreshed!.RememberMe);
        var newToken = refreshTokens.All.Single(t => t.RevokedAt == null);
        Assert.True(newToken.RememberMe);
        Assert.Equal(now.AddDays(30), newToken.ExpiresAt);
    }
}
