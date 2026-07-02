namespace SchoolMgmt.Application.Auth;

// Plain data — no HttpContext/cookie knowledge here. Setting the actual
// Set-Cookie headers is the controller's job (HTTP concern, stays in WebApi).
public record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool RememberMe,
    AuthenticatedUser User);
