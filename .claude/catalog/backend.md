<!-- Last verified: 2026-06-30. Update this file whenever a new public type/function is added or removed from the backend. Check here before adding new code — don't duplicate something that already exists. -->

# Backend Catalog

## Domain (`SchoolMgmt.Domain`)

| Type | Location | Purpose |
|---|---|---|
| `BaseEntity` | `Common/BaseEntity.cs` | Base class for all entities — `Id`, `CreatedAt`, `UpdatedAt` (audit fields stamped automatically by `AppDbContext.SaveChangesAsync`, not set manually) |
| `ITenantScoped` | `Common/ITenantScoped.cs` | Marker interface (`SchoolId`) — implement on any entity that belongs to a school. Separate from `BaseEntity` since not every entity is tenant-scoped |
| `School` | `Entities/School.cs` | The tenant entity itself — `BaseEntity` but NOT `ITenantScoped` (a school doesn't belong to itself) |

## Application (`SchoolMgmt.Application`)

| Type | Location | Purpose |
|---|---|---|
| `ITenantProvider` | `Interfaces/ITenantProvider.cs` | `CurrentSchoolId` — resolves the current tenant. Implemented today by a placeholder (`StaticTenantProvider`); will be implemented by an Auth-claims-based provider once Auth lands — no other code changes when that swap happens |
| `IDateTimeProvider` | `Interfaces/IDateTimeProvider.cs` | `UtcNow` — testable abstraction over `DateTimeOffset.UtcNow` |
| `IRepository<TEntity>` | `Interfaces/IRepository.cs` | Generic repository base (`GetByIdAsync`, `AddAsync`, `Update`, `Remove`). Per-entity repositories (e.g. `IStudentRepository`) extend this in their own feature spec — none exist yet |
| `IUnitOfWork` | `Interfaces/IUnitOfWork.cs` | Owns persistence/transactions (`SaveChangesAsync`, `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`). Repositories never call these directly |
| `DependencyInjection.AddApplication()` | `DependencyInjection.cs` | Composition-root extension method. Currently a no-op (no Application services exist yet) — feature specs register their services here |

## Infrastructure (`SchoolMgmt.Infrastructure`)

| Type | Location | Purpose |
|---|---|---|
| `AppDbContext` | `Persistence/AppDbContext.cs` | EF Core `DbContext`. Applies a global query filter to every `ITenantScoped` entity automatically (reflection over the model, not hand-written per entity). `SaveChangesAsync` is overridden to stamp `CreatedAt`/`UpdatedAt`/`SchoolId` automatically |
| `AppDbContextDesignTimeFactory` | `Persistence/AppDbContextDesignTimeFactory.cs` | `IDesignTimeDbContextFactory<AppDbContext>` — lets `dotnet ef` construct the context without the WebApi host. Reads `SCHOOLMGMT_CONNECTION_STRING` env var, falls back to a local default |
| `SchoolConfiguration` | `Persistence/Configurations/SchoolConfiguration.cs` | `IEntityTypeConfiguration<School>` — incl. the `HasData` seed for the one demo school (well-known id `00000000-0000-0000-0000-000000000001`) |
| `Repository<TEntity>` (internal) | `Persistence/Repositories/Repository.cs` | Generic `IRepository<TEntity>` implementation — touches `DbSet` only, never calls `SaveChanges`/transaction methods |
| `UnitOfWork` (internal) | `Persistence/UnitOfWork.cs` | `IUnitOfWork` implementation — wraps `AppDbContext.SaveChangesAsync` and `Database.BeginTransactionAsync`/commit/rollback |
| `StaticTenantProvider` (internal) | `MultiTenancy/StaticTenantProvider.cs` | Placeholder `ITenantProvider` — always returns the seeded school's id. Replace (not extend) when Auth lands |
| `SeedDataOptions` | `MultiTenancy/SeedDataOptions.cs` | Options-bound (`SeedData` config section); default values double as the seed migration's well-known school id/name |
| `SystemDateTimeProvider` (internal) | `Common/SystemDateTimeProvider.cs` | Real `IDateTimeProvider` — wraps `DateTimeOffset.UtcNow` |
| `DependencyInjection.AddInfrastructure()` | `DependencyInjection.cs` | Composition-root extension method — registers `AppDbContext`, `ITenantProvider`, `IDateTimeProvider`, `IUnitOfWork`, and the open-generic `IRepository<>` |

## Migrations

| Migration | Purpose |
|---|---|
| `InitialCreate` (`Persistence/Migrations/`) | Creates `Schools` table, seeds the one demo school |

## Tests

| Project | Covers |
|---|---|
| `tests/SchoolMgmt.Infrastructure.Tests` | `AppDbContext.SaveChangesAsync` audit/tenant stamping (EF Core InMemory provider, hand-written fakes, no mocking library) |
| `tests/SchoolMgmt.IntegrationTests` | Tenant query-filter isolation, `Repository`/`UnitOfWork` staging + transaction commit/rollback, seed-migration correctness, and a composition-root smoke test (`WebApplicationFactory<Program>`) — all against real Postgres via Testcontainers. Uses a test-only `ProbeEntity`/`TestDbContext` since no real tenant-scoped business entity exists yet |
