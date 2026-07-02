using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Auth;

public class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    IDateTimeProvider dateTimeProvider,
    IOptions<JwtOptions> jwtOptions)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
            return null; // same failure for "no such user" and "wrong password" — no user enumeration

        return await IssueTokensAsync(user, sessionId: Guid.NewGuid(), tokenToReplace: null, request.RememberMe, cancellationToken);
    }

    public async Task<AuthResult?> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(rawRefreshToken);
        var existing = await refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (existing is null)
            return null;

        var now = dateTimeProvider.UtcNow;

        if (existing.RevokedAt is not null)
        {
            if (existing.ReplacedByTokenId is not null)
            {
                // Already-rotated token presented again — theft signal. Revoke the whole session family.
                await RevokeSessionFamilyAsync(existing.SessionId, now, cancellationToken);
            }
            return null;
        }

        if (existing.ExpiresAt <= now)
        {
            existing.RevokedAt = now;
            refreshTokens.Update(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null; // no silent rotation of an expired token
        }

        return await IssueTokensAsync(existing.User, existing.SessionId, existing, existing.RememberMe, cancellationToken);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(rawRefreshToken);
        var existing = await refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (existing is null || existing.RevokedAt is not null)
            return; // idempotent — logging out twice (or with an unknown token) is not an error

        existing.RevokedAt = dateTimeProvider.UtcNow;
        refreshTokens.Update(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResult> IssueTokensAsync(
        User user, Guid sessionId, RefreshToken? tokenToReplace, bool rememberMe, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var rawRefreshToken = tokenGenerator.GenerateRefreshToken();
        var refreshTokenLifetime = rememberMe
            ? TimeSpan.FromDays(_jwtOptions.RefreshTokenDays)
            : TimeSpan.FromHours(_jwtOptions.SessionRefreshTokenHours);

        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            SchoolId = user.SchoolId, // explicit — ITenantProvider isn't resolvable pre-auth, see spec #2 design notes
            TokenHash = HashToken(rawRefreshToken),
            SessionId = sessionId,
            ExpiresAt = now.Add(refreshTokenLifetime),
            RememberMe = rememberMe,
        };
        await refreshTokens.AddAsync(newRefreshToken, cancellationToken);

        if (tokenToReplace is not null)
        {
            tokenToReplace.RevokedAt = now;
            tokenToReplace.ReplacedByTokenId = newRefreshToken.Id;
            refreshTokens.Update(tokenToReplace);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenGenerator.GenerateAccessToken(user);
        var accessTokenExpiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);

        return new AuthResult(
            accessToken,
            accessTokenExpiresAt,
            rawRefreshToken,
            newRefreshToken.ExpiresAt,
            rememberMe,
            new AuthenticatedUser(user.Id, user.Email, user.DisplayName, user.Role));
    }

    private async Task RevokeSessionFamilyAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await refreshTokens.GetActiveBySessionIdAsync(sessionId, cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAt = now;
            refreshTokens.Update(token);
        }
        if (active.Count > 0)
            await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
