# Spec: Implement Auth & RBAC

## Related docs & specs

- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — source decision: four roles (Admin, Teacher, Principal/Owner, Parent), parent fee-payment flow that shaped the cookie policy
- [.claude/context/architecture.md](../.claude/context/architecture.md) § Authentication — the full JWT/cookie/rotation design this spec implements; § Key Decisions & Why — `SameSite=Lax` rationale (Stripe redirect-back)
- [.claude/context/project.md](../.claude/context/project.md) — role definitions and what each role can do
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller/Application-service pattern, DI conventions, GET-must-be-read-only, Repository/UnitOfWork rules
- [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md) — **this spec directly completes it.** Spec #1 built `ITenantProvider` as an abstraction specifically so this swap could happen without touching `AppDbContext`, the query filters, or any other code — only the `AddInfrastructure` DI registration line changes. Spec #1's `StaticTenantProvider` is replaced (not extended) here. Spec #1's boundary "never bypass the query filter outside test setup code" is amended below — login/refresh lookups are a second, deliberate exception (see Design).

## Objective

Implement the JWT-in-httpOnly-cookie authentication scheme already designed in `architecture.md`, plus role-based authorization (RBAC) so future controllers can use `[Authorize(Roles = "...")]`. Concretely: a user can log in with email/password, receive access+refresh cookies, have those cookies silently refreshed, and log out (server-side revocation, not just cookie-clearing). The real, claims-based `ITenantProvider` implementation replaces spec #1's placeholder, completing the multi-tenant architecture.

Success looks like: a seeded demo Admin user can log in via `POST /api/auth/login`, the resulting cookies authenticate subsequent requests, `ITenantProvider.CurrentSchoolId` correctly resolves from the JWT claims with zero changes to `AppDbContext` or the query-filter wiring, refresh token rotation works, and presenting an already-used refresh token revokes the whole session.

**Out of scope for this spec:** any UI, user self-registration, password reset/forgot-password flow, email verification, 2FA, fine-grained per-resource authorization beyond role checks (e.g. "this teacher can only see their own classes" is enforced when the Teacher/Class features are built, not here), tenant-onboarding (still not being built — see spec #1).

## Tech Stack

- .NET 8.0, C#
- `System.IdentityModel.Tokens.Jwt` (Infrastructure) — JWT creation
- `Microsoft.AspNetCore.Authentication.JwtBearer` (WebApi) — JWT validation middleware, customized to read the cookie instead of the `Authorization` header
- `Microsoft.Extensions.Identity.Core`'s `PasswordHasher<TUser>` (Infrastructure) — password hashing only, **not** full ASP.NET Core Identity (no `UserManager`/`SignInManager`/`IdentityDbContext` — keeps the existing `Repository<T>`/`IUnitOfWork` pattern from spec #1 intact)
- `<FrameworkReference Include="Microsoft.AspNetCore.App" />` added to `SchoolMgmt.Infrastructure.csproj` — needed for `IHttpContextAccessor` (used by the new tenant provider) and `PasswordHasher<TUser>`, both shipped in the ASP.NET Core shared framework. Infrastructure is a plain class library today (per spec #1) and doesn't pull this in automatically the way `SchoolMgmt.WebApi` (an SDK.Web project) does.
- xUnit (+ `WebApplicationFactory`, Testcontainers for Postgres) — same as spec #1

## Design

### Domain (`SchoolMgmt.Domain`)

```csharp
namespace SchoolMgmt.Domain.Entities;

public class User : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

public enum UserRole
{
    Admin,
    Teacher,
    Principal, // canonical name for "Principal/Owner" — one role string, not two
    Parent,
}
```

```csharp
namespace SchoolMgmt.Domain.Entities;

public class RefreshToken : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty; // SHA-256 of the raw token — never store the raw value
    public Guid SessionId { get; set; } // groups every token issued from one login, for family revocation
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}
```

`User.Email` is unique **per school** (`SchoolId` + `Email` composite unique index) — not globally unique. See the login-lookup note below for why that still works pre-authentication.

### The pre-authentication tenant problem

Login and refresh both happen *before* `ITenantProvider` can resolve a tenant — there's no JWT/claims yet on the incoming request. Two consequences, both deliberate and documented here (not silently worked around):

1. **`IUserRepository.GetByEmailAsync` and `IRefreshTokenRepository.GetByTokenHashAsync` must bypass the tenant query filter** (`IgnoreQueryFilters()`) — this is a second, intentional exception to spec #1's boundary "never bypass the query filter outside test setup code." Both methods look up by a value that's unique regardless of tenant (email is unique per-school, but for v1 with one seeded school this is moot; `TokenHash` is globally unique by construction), so this is safe. **Spec #1's boundary list is amended** (see the diff applied to that file alongside this one) to read: "...outside test setup code, or the two pre-auth lookups in `specs/02-implement-auth.md` (`GetByEmailAsync`, `GetByTokenHashAsync`)."
2. **`AuthService` sets `RefreshToken.SchoolId` explicitly** from the looked-up `User.SchoolId` when creating a new refresh token at login — it does NOT rely on `AppDbContext.SaveChangesAsync`'s automatic tenant-stamping (which reads `ITenantProvider.CurrentSchoolId`, unresolvable at this point since the request isn't authenticated yet). This is safe per spec #1's stamping logic: it only fills `SchoolId` when it's still `Guid.Empty`, so an explicitly-set value is never overwritten.

### Tenant resolution — the real implementation (`SchoolMgmt.Infrastructure`)

Replaces `StaticTenantProvider`. Reads `SchoolId` from the authenticated user's claims via `IHttpContextAccessor`:

```csharp
namespace SchoolMgmt.Infrastructure.MultiTenancy;

internal sealed class HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public Guid CurrentSchoolId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst("school_id")
                ?? throw new InvalidOperationException(
                    "No authenticated tenant context. CurrentSchoolId must only be accessed on authenticated, tenant-scoped requests.");
            return Guid.Parse(claim.Value);
        }
    }
}
```

Throwing (rather than silently returning `Guid.Empty`) is deliberate: a tenant-scoped query running with no resolvable tenant is a bug that should surface loudly, not silently return zero rows or match accidentally-empty-Guid data.

**The only change anywhere else in the codebase is the DI registration line** in `Infrastructure/DependencyInjection.cs`:
```csharp
// Before (spec #1):
services.AddScoped<ITenantProvider, StaticTenantProvider>();
// After (this spec):
services.AddScoped<ITenantProvider, HttpContextTenantProvider>();
services.AddHttpContextAccessor();
```
`AppDbContext`, the reflection-based query filter wiring, and every other consumer of `ITenantProvider` are untouched — this is the payoff of spec #1's abstraction.

### JWT issuance (`SchoolMgmt.Infrastructure`)

```csharp
namespace SchoolMgmt.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken(); // raw, high-entropy random string — not a JWT
}
```

Access token claims: `sub` (user id), `email`, `school_id`, `ClaimTypes.Role` (so `[Authorize(Roles = "Admin")]` works natively), `name` (display name). Signed HMAC-SHA256 with a key from configuration (`Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` = 15, `Jwt:RefreshTokenDays` = 30, `Jwt:SessionRefreshTokenHours` = 24). Refresh tokens are NOT JWTs — a cryptographically random string (`RandomNumberGenerator`, 256 bits, base64url), stored only as a SHA-256 hash (fast hash is correct here — refresh tokens are already high-entropy secrets, unlike passwords).

`Jwt:RefreshTokenDays` applies when `RememberMe = true`. `Jwt:SessionRefreshTokenHours` applies when `RememberMe = false` — a shorter server-side lifetime combined with a session cookie (no `Expires`) means the token is invalidated both when the browser closes and after the configured hours, whichever comes first.

### Cookie-based JWT validation (`SchoolMgmt.WebApi`)

`AddAuthentication().AddJwtBearer(...)` configured with `OnMessageReceived` to pull the token from the `access_token` cookie instead of the default `Authorization` header — required because the documented design never puts the token in a header:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        if (context.Request.Cookies.TryGetValue("access_token", out var token))
            context.Token = token;
        return Task.CompletedTask;
    }
};
```

### Application layer (`SchoolMgmt.Application`)

One service per the established pattern — `AuthService`, called directly from a thin `AuthController`:

```csharp
public class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator)
{
    public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct);
    public Task<AuthResult> RefreshAsync(string rawRefreshToken, CancellationToken ct);
    public Task LogoutAsync(string rawRefreshToken, CancellationToken ct);
}
```

`LoginRequest` includes `RememberMe`:

```csharp
public record LoginRequest(string Email, string Password, bool RememberMe = false);
```

`AuthResult` carries the new access token string, the new raw refresh token string, their expiries, and the `RememberMe` flag — plain data, no `HttpContext`/cookie knowledge in the Application layer. `LoginAsync` computes `RefreshTokenExpiresAt` as `now + RefreshTokenDays` when `RememberMe = true`, or `now + SessionRefreshTokenHours` when `false`. **Setting the actual `Set-Cookie` headers is the controller's job** (HTTP concern, stays in WebApi):

```csharp
[AllowAnonymous, HttpPost("login")]
public async Task<IActionResult> Login(LoginRequest request)
{
    var result = await _authService.LoginAsync(request);
    SetAuthCookies(result); // httpOnly, Secure, SameSite=Lax — see architecture.md
    return Ok(new { result.User });
}
```

`SetAuthCookies` behaviour depends on `result.RememberMe`:
- `true` → both cookies get `Expires = <token expiry>` (persistent — survives browser restart)
- `false` → `Expires` is **omitted** on both cookies (session cookies — cleared when the browser closes), even though the refresh token has a server-side expiry of `SessionRefreshTokenHours`

`IPasswordHasher` (Application interface, implemented in Infrastructure wrapping `PasswordHasher<User>`) — same interfaces-live-where-consumed rule as everything else in this codebase.

### Endpoints

All POST (per the GET-must-be-read-only rule — none of these are safe/idempotent) except `/me`:

| Route | Auth | Purpose |
|---|---|---|
| `POST /api/auth/login` | Anonymous | Validate credentials, issue cookies |
| `POST /api/auth/refresh` | Anonymous | Reads `refresh_token` cookie, rotates, issues new cookies. Must be anonymous — the whole point is to work when the access token has already expired |
| `POST /api/auth/logout` | Anonymous | Reads `refresh_token` cookie, revokes it server-side, clears both cookies. Anonymous for the same reason as refresh |
| `GET /api/auth/me` | `[Authorize]` | Returns the current user's id/email/displayName/role from claims — read-only, doubles as a smoke-test endpoint for the whole flow and as the integration tests' way to verify `ITenantProvider` resolves correctly without needing a real business endpoint to exist yet |

### Refresh rotation & theft detection

On `POST /api/auth/refresh`:
1. Hash the presented raw token, look up by `TokenHash` (`IgnoreQueryFilters()` — see above).
2. If not found → 401.
3. If found but `RevokedAt` is set **and** it has a `ReplacedByTokenId` (i.e. it was already legitimately rotated, and is now being replayed) → **revoke every token in that `SessionId` family** (theft signal), then 401.
4. If found, not revoked, not expired → mark it revoked, set `ReplacedByTokenId` to the new token's id, issue a new access+refresh pair under the same `SessionId`, persist via `IUnitOfWork.SaveChangesAsync()`.
5. If found but expired (and not yet revoked) → revoke it, 401 (no silent rotation of an expired token).

### Seed demo user

Extends `SeedDataOptions` (from spec #1) with `DefaultAdminEmail` and `DefaultAdminPasswordHash`. A new migration adds `Users`/`RefreshTokens` tables and seeds one `Admin` user tied to the seeded `School` from spec #1, via `HasData` (same pattern as the school seed — anonymous object, not a `User` instance, since `CreatedAt` is private-set). The password hash is computed once at spec-authoring/implementation time via `PasswordHasher<User>` and hardcoded into the seed (same approach Identity-scaffolded templates use for seed data) — **the plaintext demo password must be documented in `.claude/context/project.md` once implemented**, since it's the only way to actually log into the demo.

## Project Structure

```
backend/
  SchoolMgmt.Domain/
    Entities/
      User.cs
      RefreshToken.cs
      UserRole.cs (enum)
  SchoolMgmt.Application/
    Interfaces/
      IUserRepository.cs       # extends IRepository<User>: GetByEmailAsync (IgnoreQueryFilters)
      IRefreshTokenRepository.cs # extends IRepository<RefreshToken>: GetByTokenHashAsync (IgnoreQueryFilters)
      IPasswordHasher.cs
      IJwtTokenGenerator.cs
    Auth/
      AuthService.cs
      LoginRequest.cs / AuthResult.cs (DTOs)
  SchoolMgmt.Infrastructure/
    MultiTenancy/
      HttpContextTenantProvider.cs   # replaces StaticTenantProvider
    Auth/
      PasswordHasherAdapter.cs       # IPasswordHasher wrapping PasswordHasher<User>
      JwtTokenGenerator.cs
    Persistence/
      Repositories/
        UserRepository.cs
        RefreshTokenRepository.cs
      Configurations/
        UserConfiguration.cs         # incl. HasData seed, unique index (SchoolId, Email)
        RefreshTokenConfiguration.cs
      Migrations/                    # new migration: AddUsersAndRefreshTokens
  SchoolMgmt.WebApi/
    Controllers/
      AuthController.cs
    Program.cs                       # AddAuthentication().AddJwtBearer(...) + AddAuthorization()
tests/
  SchoolMgmt.Infrastructure.Tests/
    Auth/
      AuthServiceTests.cs            # fakes for IUserRepository/IRefreshTokenRepository/IPasswordHasher/IJwtTokenGenerator
      JwtTokenGeneratorTests.cs      # decodes generated token, asserts claims
  SchoolMgmt.IntegrationTests/
    Auth/
      LoginTests.cs
      RefreshRotationTests.cs        # incl. theft-detection scenario
      LogoutTests.cs
      TenantResolutionTests.cs       # confirms HttpContextTenantProvider resolves SchoolId end-to-end
```

## Code Style

Follows [.claude/rules/backend.md](../.claude/rules/backend.md): thin controller → one `AuthService` method, no MediatR, `IUnitOfWork.SaveChangesAsync()` called once per use case, repositories never call `SaveChanges`/transaction methods, interfaces defined in Application and implemented in Infrastructure, `Scoped` lifetime by default.

## Testing Strategy

Per [.claude/context/architecture.md § Testing](../.claude/context/architecture.md):

- **Unit (xUnit, hand-written fakes, no mocking library):**
  - `AuthService.LoginAsync` — correct credentials return a token pair; wrong password returns a failure result without revealing whether the email exists; nonexistent email behaves identically to wrong password (no user enumeration).
  - `AuthService.LoginAsync` with `RememberMe = true` → `RefreshTokenExpiresAt` is `now + RefreshTokenDays`; with `RememberMe = false` → `RefreshTokenExpiresAt` is `now + SessionRefreshTokenHours`.
  - `AuthService.RefreshAsync` — valid token rotates and returns a new pair under the same `SessionId`; reused (already-replaced) token revokes the entire session family; expired token is rejected and revoked.
  - `JwtTokenGenerator.GenerateAccessToken` — decoded token contains `sub`, `school_id`, role claim with the expected values.
- **Integration (xUnit + `WebApplicationFactory` + Testcontainers/Postgres):**
  - `POST /api/auth/login` with `rememberMe: true` → cookies have `Expires` set; with `rememberMe: false` → cookies have no `Expires` (session cookies). Both variants: `HttpOnly`, `Secure`, `SameSite=Lax`.
  - `GET /api/auth/me` with the login cookies attached → 200, correct user info; confirms `HttpContextTenantProvider.CurrentSchoolId` resolves correctly with zero other code changes (the spec #1 payoff).
  - `POST /api/auth/refresh` with a valid refresh cookie → new cookies issued; the OLD refresh token can no longer be used.
  - Replaying an already-rotated refresh token → that whole session family (incl. the most recently issued token) is revoked; subsequent refresh attempts with any token from that family fail.
  - `POST /api/auth/logout` → refresh token revoked server-side; a subsequent refresh attempt with it fails.
  - Wrong password → 401, generic error message (no user enumeration).

## Boundaries

- **Always:** run the full test suite before considering a task in this spec done; never store a raw refresh token (hash only); never put the JWT secret in source control (config/env var only, dev placeholder in `appsettings.Development.json` clearly marked as dev-only).
- **Ask first:** changing token lifetimes meaningfully (e.g. >1hr access tokens), changing the role enum's string values once any other spec depends on them, adopting full ASP.NET Core Identity instead of the standalone `PasswordHasher<TUser>` approach.
- **Never:** implement password reset/self-registration/2FA in this spec (explicitly out of scope); put the access or refresh token anywhere accessible to JavaScript; bypass the tenant query filter anywhere other than the two documented pre-auth lookups; make `/login`, `/refresh`, or `/logout` a GET endpoint; let `AuthService` (Application layer) touch `HttpContext` or cookies directly.

## Success Criteria

- `User`/`RefreshToken` entities exist, both `ITenantScoped`; migration creates their tables and seeds one demo Admin user tied to the spec #1 school.
- `HttpContextTenantProvider` replaces `StaticTenantProvider` via a single DI registration line change — no other file touches `AppDbContext` or the query-filter wiring.
- Login issues correctly-attributed cookies (`HttpOnly`, `Secure`, `SameSite=Lax`) containing a valid JWT with `school_id` and role claims; `rememberMe = true` produces persistent cookies with `Expires`, `rememberMe = false` produces session cookies without `Expires`.
- Refresh rotates tokens correctly and detects/punishes reuse by revoking the entire session family.
- Logout revokes server-side, not just client-side.
- `[Authorize(Roles = "...")]` is usable by any future controller without further plumbing.
- All tests listed under Testing Strategy pass.

## Open Questions

None remaining. Both flagged items from the spec process are resolved: password hashing uses standalone `PasswordHasher<TUser>` (not full Identity), and this spec seeds one demo Admin user — the plaintext demo password gets documented in `.claude/context/project.md` once implemented (not before, since the hash doesn't exist until the migration is written).
