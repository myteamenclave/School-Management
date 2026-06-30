<!-- Last verified: 2026-06-30. Update this file whenever a new public type/function is added or removed from the backend. Check here before adding new code — don't duplicate something that already exists. -->

# Backend Catalog

## Domain (`SchoolMgmt.Domain`)

| Type | Location | Purpose |
|---|---|---|
| `BaseEntity` | `Common/BaseEntity.cs` | Base class for all entities — `Id`, `CreatedAt`, `UpdatedAt` (audit fields stamped automatically by `AppDbContext.SaveChangesAsync`, not set manually) |
| `ITenantScoped` | `Common/ITenantScoped.cs` | Marker interface (`SchoolId`) — implement on any entity that belongs to a school. Separate from `BaseEntity` since not every entity is tenant-scoped |
| `School` | `Entities/School.cs` | The tenant entity itself — `BaseEntity` but NOT `ITenantScoped` (a school doesn't belong to itself) |
| `User` | `Entities/User.cs` | `BaseEntity` + `ITenantScoped`. Email (unique per school), password hash, display name, `Role` |
| `UserRole` (enum) | `Entities/UserRole.cs` | `Admin`, `Teacher`, `Principal` (canonical name for "Principal/Owner"), `Parent` |
| `RefreshToken` | `Entities/RefreshToken.cs` | `BaseEntity` + `ITenantScoped`. Hashed token, `SessionId` (groups tokens from one login for family revocation), `ExpiresAt`/`RevokedAt`/`ReplacedByTokenId`, `User` navigation (eager-loaded by `GetByTokenHashAsync`) |

## Application (`SchoolMgmt.Application`)

| Type | Location | Purpose |
|---|---|---|
| `ITenantProvider` | `Interfaces/ITenantProvider.cs` | `CurrentSchoolId` — resolves the current tenant. Implemented by `HttpContextTenantProvider` (real, claims-based) at runtime; `StaticTenantProvider` only at EF Core design-time (migrations have no `HttpContext`) |
| `IDateTimeProvider` | `Interfaces/IDateTimeProvider.cs` | `UtcNow` — testable abstraction over `DateTimeOffset.UtcNow` |
| `IRepository<TEntity>` | `Interfaces/IRepository.cs` | Generic repository base (`GetByIdAsync`, `AddAsync`, `Update`, `Remove`). Per-entity repositories extend this — `IUserRepository`, `IRefreshTokenRepository` exist; more land with future feature specs |
| `IUnitOfWork` | `Interfaces/IUnitOfWork.cs` | Owns persistence/transactions (`SaveChangesAsync`, `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`). Repositories never call these directly |
| `IUserRepository` | `Interfaces/IUserRepository.cs` | Extends `IRepository<User>`. `GetByEmailAsync` — bypasses the tenant filter (login is pre-authentication) |
| `IRefreshTokenRepository` | `Interfaces/IRefreshTokenRepository.cs` | Extends `IRepository<RefreshToken>`. `GetByTokenHashAsync` (eager-loads `User`, bypasses tenant filter), `GetActiveBySessionIdAsync` (for theft-detection family revocation) |
| `IPasswordHasher` | `Interfaces/IPasswordHasher.cs` | `HashPassword`/`VerifyPassword`. Implemented by `PasswordHasherAdapter` wrapping ASP.NET Core Identity's standalone `PasswordHasher<TUser>` (not full Identity) |
| `IJwtTokenGenerator` | `Interfaces/IJwtTokenGenerator.cs` | `GenerateAccessToken(User)` (JWT), `GenerateRefreshToken()` (raw random string, not a JWT) |
| `JwtOptions` | `Auth/JwtOptions.cs` | Config POCO (`Jwt` section) — `Secret`/`Issuer`/`Audience`/`AccessTokenMinutes`/`RefreshTokenDays`. Shared by `AuthService` (Application) and `JwtTokenGenerator`/JWT-bearer validation (Infrastructure/WebApi) |
| `AuthService` | `Auth/AuthService.cs` | `LoginAsync`, `RefreshAsync` (rotation + theft-family-revocation), `LogoutAsync`. One service per the established Application-service pattern |
| `LoginRequest` / `AuthResult` / `AuthenticatedUser` | `Auth/*.cs` | Plain DTOs — `AuthResult` has zero `HttpContext`/cookie knowledge; the controller sets cookies |
| `DependencyInjection.AddApplication()` | `DependencyInjection.cs` | Composition-root extension method — registers `AuthService` |

## Infrastructure (`SchoolMgmt.Infrastructure`)

| Type | Location | Purpose |
|---|---|---|
| `AppDbContext` | `Persistence/AppDbContext.cs` | EF Core `DbContext`. Applies a global query filter to every `ITenantScoped` entity automatically (reflection over the model, not hand-written per entity). `SaveChangesAsync` is overridden to stamp `CreatedAt`/`UpdatedAt`/`SchoolId` automatically. `DbSet`s: `Schools`, `Users`, `RefreshTokens` |
| `AppDbContextDesignTimeFactory` | `Persistence/AppDbContextDesignTimeFactory.cs` | `IDesignTimeDbContextFactory<AppDbContext>` — lets `dotnet ef` construct the context without the WebApi host. Reads `SCHOOLMGMT_CONNECTION_STRING` env var, falls back to a local default. Uses `StaticTenantProvider` (no `HttpContext` at design time) |
| `SchoolConfiguration` | `Persistence/Configurations/SchoolConfiguration.cs` | `IEntityTypeConfiguration<School>` — incl. the `HasData` seed for the one demo school (well-known id `00000000-0000-0000-0000-000000000001`) |
| `UserConfiguration` | `Persistence/Configurations/UserConfiguration.cs` | `IEntityTypeConfiguration<User>` — unique index `(SchoolId, Email)`, `Role` stored as string. Deliberately NO `HasData` seed — see `DemoDataSeeder` |
| `RefreshTokenConfiguration` | `Persistence/Configurations/RefreshTokenConfiguration.cs` | `IEntityTypeConfiguration<RefreshToken>` — unique index on `TokenHash`, FK to `User` (cascade delete) |
| `Repository<TEntity>` (internal) | `Persistence/Repositories/Repository.cs` | Generic `IRepository<TEntity>` implementation — touches `DbSet` only, never calls `SaveChanges`/transaction methods |
| `UserRepository` (internal) | `Persistence/Repositories/UserRepository.cs` | `IUserRepository` implementation — `GetByEmailAsync` uses `IgnoreQueryFilters()` |
| `RefreshTokenRepository` (internal) | `Persistence/Repositories/RefreshTokenRepository.cs` | `IRefreshTokenRepository` implementation — `IgnoreQueryFilters()` + `.Include(rt => rt.User)` |
| `UnitOfWork` (internal) | `Persistence/UnitOfWork.cs` | `IUnitOfWork` implementation — wraps `AppDbContext.SaveChangesAsync` and `Database.BeginTransactionAsync`/commit/rollback |
| `StaticTenantProvider` (internal) | `MultiTenancy/StaticTenantProvider.cs` | `ITenantProvider` for EF Core design-time tooling only (migrations have no `HttpContext`) — always returns the seeded school's id |
| `HttpContextTenantProvider` (internal) | `MultiTenancy/HttpContextTenantProvider.cs` | Real runtime `ITenantProvider` — reads `school_id` from the authenticated user's claims via `IHttpContextAccessor`. Throws if accessed with no authenticated request (deliberate — surfaces bugs loudly) |
| `SeedDataOptions` | `MultiTenancy/SeedDataOptions.cs` | Options-bound (`SeedData` config section); default values double as the well-known school id/name (seed migration) and admin-user id/email/displayName/hash (`DemoDataSeeder`, runtime, not migration) |
| `DemoDataSeeder` | `Persistence/DemoDataSeeder.cs` | `IServiceProvider.SeedDemoDataAsync(IHostEnvironment)` extension — seeds the demo Admin user at app startup, gated by `IsDevelopment()`. Idempotent (checked by email). NOT wired via migration `HasData` — see Key Decision in architecture.md |
| `SystemDateTimeProvider` (internal) | `Common/SystemDateTimeProvider.cs` | Real `IDateTimeProvider` — wraps `DateTimeOffset.UtcNow` |
| `PasswordHasherAdapter` (internal) | `Auth/PasswordHasherAdapter.cs` | `IPasswordHasher` wrapping `Microsoft.AspNetCore.Identity.PasswordHasher<User>` |
| `JwtTokenGenerator` (internal) | `Auth/JwtTokenGenerator.cs` | `IJwtTokenGenerator` implementation — HMAC-SHA256 signed JWTs via `System.IdentityModel.Tokens.Jwt` |
| `DependencyInjection.AddInfrastructure()` | `DependencyInjection.cs` | Composition-root extension method — registers `AppDbContext`, `ITenantProvider` (→ `HttpContextTenantProvider`), `IDateTimeProvider`, `IUnitOfWork`, `IRepository<>`, `IUserRepository`, `IRefreshTokenRepository`, `IPasswordHasher`, `IJwtTokenGenerator` |

## WebApi (`SchoolMgmt.WebApi`)

| Type | Location | Purpose |
|---|---|---|
| `AuthController` | `Controllers/AuthController.cs` | `POST /api/auth/login`, `/refresh`, `/logout` (anonymous, all side-effecting → POST), `GET /api/auth/me` (`[Authorize]`, read-only). Sets/clears `access_token`/`refresh_token` httpOnly cookies — the only place HTTP/cookie concerns touch auth |
| `Program.cs` JWT wiring | `Program.cs` | `AddAuthentication().AddJwtBearer()` + `AddOptions<JwtBearerOptions>().Configure<IOptions<JwtOptions>>(...)` (lazy DI-resolved binding — see Key Decision in architecture.md about why eager config reads broke under `WebApplicationFactory`). `MapInboundClaims = false`. Reads the access token from the `access_token` cookie via `OnMessageReceived`, not the `Authorization` header |
| `Program.cs` health check | `Program.cs` | `GET /health` (`AddHealthChecks().AddDbContextCheck<AppDbContext>()`) — anonymous, verifies real DB connectivity. Not wired into docker-compose (no compose-level healthcheck on `api`), available for external monitoring |
| `Program.cs` demo seeding | `Program.cs` | `await app.Services.SeedDemoDataAsync(app.Environment)` right after `app.Build()` — see `DemoDataSeeder` |

## Migrations

| Migration | Purpose |
|---|---|
| `InitialCreate` (`Persistence/Migrations/`) | Creates `Schools` table, seeds the one demo school |
| `AddUsersAndRefreshTokens` (`Persistence/Migrations/`) | Creates `Users`/`RefreshTokens` tables. No seed data — the demo Admin user is seeded at runtime instead, see `DemoDataSeeder` |

## Tests

| Project | Covers |
|---|---|
| `tests/SchoolMgmt.Infrastructure.Tests` | `AppDbContext.SaveChangesAsync` audit/tenant stamping; `AuthService` (login/refresh-rotation/theft-detection/expiry) via hand-written fakes; `JwtTokenGenerator` claim correctness. EF Core InMemory provider where a `DbContext` is needed, no mocking library anywhere |
| `tests/SchoolMgmt.IntegrationTests` | Tenant query-filter isolation, `Repository`/`UnitOfWork` staging + transaction commit/rollback, seed-migration correctness, full HTTP auth flows (`LoginTests`, `RefreshRotationTests` incl. theft-family-revocation, `LogoutTests`, `TenantResolutionTests` — proves `HttpContextTenantProvider` resolves `SchoolId` on a real authenticated request), and a composition-root smoke test — all against real Postgres via Testcontainers. `PostgresContainerFixture.CreateFactory()` is the shared `WebApplicationFactory<Program>` builder (overrides connection string + JWT config, explicitly sets `UseEnvironment("Development")` so `DemoDataSeeder` actually runs — `WebApplicationFactory` defaults to `Production` otherwise) reused across all HTTP-level tests |
