using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Auth;
using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, ITenantProvider tenantProvider) : ControllerBase
{
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        if (result is null)
            return Unauthorized(new { error = "Invalid email or password." });

        SetAuthCookies(result);
        return Ok(new { result.User });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookie, out var rawRefreshToken) || string.IsNullOrEmpty(rawRefreshToken))
            return Unauthorized();

        var result = await authService.RefreshAsync(rawRefreshToken, cancellationToken);
        if (result is null)
        {
            ClearAuthCookies();
            return Unauthorized();
        }

        SetAuthCookies(result);
        return Ok(new { result.User });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshTokenCookie, out var rawRefreshToken) && !string.IsNullOrEmpty(rawRefreshToken))
            await authService.LogoutAsync(rawRefreshToken, cancellationToken);

        ClearAuthCookies();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        // Resolving ITenantProvider here (rather than just reading the
        // school_id claim directly) is deliberate — it exercises
        // HttpContextTenantProvider end-to-end on a real authenticated
        // request, which is how the integration tests verify it resolves
        // correctly. See specs/02-implement-auth.md "Endpoints".
        return Ok(new
        {
            Id = User.FindFirstValue("sub"),
            Email = User.FindFirstValue("email"),
            DisplayName = User.FindFirstValue("name"),
            Role = User.FindFirstValue(ClaimTypes.Role),
            SchoolId = tenantProvider.CurrentSchoolId,
        });
    }

    private void SetAuthCookies(AuthResult result)
    {
        var accessOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        };
        if (result.RememberMe) accessOptions.Expires = result.AccessTokenExpiresAt;
        Response.Cookies.Append(AccessTokenCookie, result.AccessToken, accessOptions);

        // Scoped to /api/auth only — the refresh token never needs to be sent
        // on ordinary business-endpoint requests, unlike the access token.
        var refreshOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
        };
        if (result.RememberMe) refreshOptions.Expires = result.RefreshTokenExpiresAt;
        Response.Cookies.Append(RefreshTokenCookie, result.RefreshToken, refreshOptions);
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AccessTokenCookie, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions { Path = "/api/auth" });
    }
}
