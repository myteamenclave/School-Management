# Spec: Implement Multi-Tenant Data Model

## Related docs & specs

- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — source decision: "multi-tenancy is treated as an architecture decision, not a feature... architected for it, don't build the product surface"
- [.claude/context/architecture.md](../.claude/context/architecture.md) — "Multi-tenancy architected, not built" (Key Decisions & Why), Tech Stack, Database migrations subsection
- [.claude/context/database.md](../.claude/context/database.md) — engine, schema approach, `SchoolId` notes
- [.claude/rules/backend.md](../.claude/rules/backend.md) — DI conventions (per-layer `DependencyInjection.cs`, interfaces live where consumed, `Scoped` default lifetime), no-mediator/thin-controller pattern
- No prior specs — this is the first spec in the project.

## Objective

Establish the foundational data-access layer every future feature depends on: a base entity convention, real per-tenant query isolation via EF Core global query filters, a tenant-resolution abstraction that doesn't need to change when Auth (the next spec) is implemented, and the Repository + Unit of Work pattern (per [.claude/rules/backend.md](../.claude/rules/backend.md)) that every feature's data access goes through.

This spec does **not** implement any business entity (Student, Staff, Fee, etc.) or any entity-specific repository (e.g. `IStudentRepository`) — those each get their own spec and build on top of what's defined here. Success here means: a developer adding a new tenant-scoped entity next week only has to (a) inherit the base class, (b) implement the marker interface, and (c) define a thin per-entity repository interface extending the generic base — no new query-filter, persistence, or transaction wiring required.

**Out of scope for this spec:** tenant-onboarding UI, super-admin console, per-tenant branding (per the architecture decision — not being built in v1), and the real `ITenantProvider` implementation that reads from JWT claims (that lands with the Auth spec).

## Tech Stack

- .NET 8.0, C#
- Entity Framework Core (PostgreSQL provider, `Npgsql.EntityFrameworkCore.PostgreSQL`)
- xUnit (+ `WebApplicationFactory`, Testcontainers for Postgres)
- Existing solution structure: `SchoolMgmt.Domain`, `SchoolMgmt.Application`, `SchoolMgmt.Infrastructure`, `SchoolMgmt.WebApi` (all currently empty scaffolding)

## Design

### Base entity conventions (`SchoolMgmt.Domain`)

```csharp
namespace SchoolMgmt.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Called only from DbContext.SaveChangesAsync — see "Audit field automation" below.
    internal void SetCreated(DateTimeOffset now) => CreatedAt = now;
    internal void SetUpdated(DateTimeOffset now) => UpdatedAt = now;
}

public interface ITenantScoped
{
    Guid SchoolId { get; set; }
}
```

`ITenantScoped` is a separate interface, not folded into `BaseEntity`, because not every future entity is guaranteed to need tenant scoping (e.g. a possible future system-level lookup table). Every entity introduced by a feature spec must inherit `BaseEntity`; only entities that belong to a school also implement `ITenantScoped`. In practice, almost everything will (Students, Staff, Fees, etc.), but the distinction is enforced explicitly per entity rather than assumed.

### Tenant resolution (`ITenantProvider`)

Defined in `SchoolMgmt.Application` (consumed by Infrastructure's `DbContext`, per the "interfaces live where consumed" rule):

```csharp
namespace SchoolMgmt.Application.Interfaces;

public interface ITenantProvider
{
    Guid CurrentSchoolId { get; }
}
```

For this spec, `SchoolMgmt.Infrastructure` provides a placeholder implementation that always returns the one seeded school's id:

```csharp
namespace SchoolMgmt.Infrastructure.MultiTenancy;

internal sealed class StaticTenantProvider(IOptions<SeedDataOptions> seedOptions) : ITenantProvider
{
    public Guid CurrentSchoolId => seedOptions.Value.DefaultSchoolId;
}
```

When the Auth spec lands, this is replaced by an implementation that reads `SchoolId` from the authenticated user's claims (e.g. `HttpContextTenantProvider`). **No other code in this spec — the `DbContext`, the query filters, the `DependencyInjection.cs` registration call site — changes when that swap happens.** Only the `AddInfrastructure` registration line (`services.AddScoped<ITenantProvider, ...>()`) is updated to point at the new implementation.

### EF Core global query filters (`SchoolMgmt.Infrastructure`)

`AppDbContext` applies a query filter to every `ITenantScoped` entity type discovered in the model, via reflection in `OnModelCreating` — not hand-written per entity, so a new entity gets tenant isolation automatically just by implementing the interface:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType)) continue;

        var method = SetTenantFilterMethod.MakeGenericMethod(entityType.ClrType);
        method.Invoke(this, new object[] { modelBuilder });
    }
}

private static readonly MethodInfo SetTenantFilterMethod =
    typeof(AppDbContext).GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped
{
    modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.SchoolId == _tenantProvider.CurrentSchoolId);
}
```

### Audit field + tenant assignment automation

`AppDbContext.SaveChangesAsync` is overridden to set `CreatedAt`/`UpdatedAt` and, for newly-added `ITenantScoped` entities, `SchoolId` — automatically, so application/service code never has to remember to set these manually (which is exactly the kind of thing that silently creates a cross-tenant row if forgotten):

```csharp
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    var now = _dateTimeProvider.UtcNow;

    foreach (var entry in ChangeTracker.Entries<BaseEntity>())
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.SetCreated(now);
            if (entry.Entity is ITenantScoped tenantScoped && tenantScoped.SchoolId == Guid.Empty)
                tenantScoped.SchoolId = _tenantProvider.CurrentSchoolId;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Entity.SetUpdated(now);
        }
    }

    return base.SaveChangesAsync(cancellationToken);
}
```

(`IDateTimeProvider` is a small abstraction over `DateTimeOffset.UtcNow` — included here for testability, not specified further since it's a one-line interface.)

### Repository pattern & Unit of Work

Per [.claude/rules/backend.md](../.claude/rules/backend.md): repository interfaces and `IUnitOfWork` are defined in `SchoolMgmt.Application`; implementations live in `SchoolMgmt.Infrastructure`. This spec establishes the **generic base only** — per-entity repositories (e.g. `IStudentRepository`) are defined in each feature's own spec, extending the generic interface below.

```csharp
namespace SchoolMgmt.Application.Interfaces;

public interface IRepository<TEntity> where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

`SchoolMgmt.Infrastructure` implementations:

```csharp
namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal class Repository<TEntity>(AppDbContext context) : IRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly DbSet<TEntity> DbSet = context.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        DbSet.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TEntity entity) => DbSet.Update(entity);

    public void Remove(TEntity entity) => DbSet.Remove(entity);
}
```

```csharp
namespace SchoolMgmt.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        _transaction = await context.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
```

`Repository<TEntity>` never calls `SaveChanges`/`SaveChangesAsync` or any transaction method — it only touches `DbSet`. `UnitOfWork.SaveChangesAsync` delegates straight to `AppDbContext.SaveChangesAsync`, so the audit-field/tenant-stamping override above still runs on every commit, regardless of how many repositories were touched first.

### Seed school

A `Schools` table (`Id`, `Name`, `CreatedAt`) is added — itself a `BaseEntity` but **not** `ITenantScoped` (a school doesn't belong to itself). One row is seeded via EF Core's `HasData` in the initial migration, consistent with the already-documented migration flow (local: `IDesignTimeDbContextFactory`; deployment: Docker `migrator` stage — see [architecture.md § Database migrations](../.claude/context/architecture.md#database-migrations)). The seeded id is read by `StaticTenantProvider` via `SeedDataOptions` (bound from configuration, defaulting to a fixed well-known GUID so it's identical across environments without needing a lookup).

### Dependency injection

Per [.claude/rules/backend.md](../.claude/rules/backend.md):
- `SchoolMgmt.Infrastructure/DependencyInjection.cs` → `AddInfrastructure(IServiceCollection, IConfiguration)` registers `AppDbContext` (`AddDbContext`, `Scoped` by default), `ITenantProvider` → `StaticTenantProvider` (`Scoped`), `IDateTimeProvider` → its implementation (`Singleton` — genuinely stateless), `IUnitOfWork` → `UnitOfWork` (`Scoped`), and the open generic `typeof(IRepository<>)` → `typeof(Repository<>)` (`Scoped`) so any future `IRepository<TEntity>` resolves automatically without a per-entity registration line.
- `SchoolMgmt.WebApi/Program.cs` composes: `builder.Services.AddInfrastructure(builder.Configuration);` — no direct registrations of Infrastructure types in `Program.cs`.

## Project Structure

```
backend/
  SchoolMgmt.Domain/
    Common/
      BaseEntity.cs
      ITenantScoped.cs
  SchoolMgmt.Application/
    Interfaces/
      ITenantProvider.cs
      IDateTimeProvider.cs
      IRepository.cs
      IUnitOfWork.cs
  SchoolMgmt.Infrastructure/
    Persistence/
      AppDbContext.cs
      AppDbContextDesignTimeFactory.cs   # IDesignTimeDbContextFactory<AppDbContext>
      UnitOfWork.cs
      Configurations/
        SchoolConfiguration.cs           # IEntityTypeConfiguration<School>, incl. HasData seed
      Migrations/                        # EF Core-generated
      Repositories/
        Repository.cs                    # generic IRepository<TEntity> implementation
    MultiTenancy/
      StaticTenantProvider.cs
    Common/
      SystemDateTimeProvider.cs
    DependencyInjection.cs
  SchoolMgmt.WebApi/
    Program.cs                           # calls AddInfrastructure
tests/
  SchoolMgmt.Infrastructure.Tests/       # xUnit unit tests, hand-written fakes (no mocking library)
  SchoolMgmt.IntegrationTests/           # xUnit + WebApplicationFactory + Testcontainers/Postgres
```

## Commands

```
Build:   dotnet build SchoolMgmt.slnx
Test:    dotnet test SchoolMgmt.slnx
Migrate (local): dotnet ef database update --project SchoolMgmt.Infrastructure
Add migration:   dotnet ef migrations add <Name> --project SchoolMgmt.Infrastructure
```

## Code Style

Follows [.claude/rules/backend.md](../.claude/rules/backend.md): no mediator library, `Scoped` by default, constructor injection only, interfaces defined where consumed. New entities in future specs follow the `BaseEntity` / `ITenantScoped` pattern shown above without modification to this spec's code.

## Testing Strategy

Per [.claude/context/architecture.md § Testing](../.claude/context/architecture.md):

- **Unit (`SchoolMgmt.Infrastructure.Tests`, xUnit):** the reflection-based query-filter wiring in `OnModelCreating` is exercised indirectly via the integration tests below (it's EF Core model configuration, not pure logic) — unit tests instead cover `SaveChangesAsync`'s audit-field/tenant-assignment behavior using a hand-written fake `ITenantProvider` and fake `IDateTimeProvider` against an `AppDbContext` (no mocking library, per the testing convention).
- **Integration (`SchoolMgmt.IntegrationTests`, xUnit + `WebApplicationFactory` + Testcontainers/Postgres):**
  - A query against a tenant-scoped entity only returns rows matching `ITenantProvider.CurrentSchoolId` — seed two schools' worth of data directly (bypassing the filter via `IgnoreQueryFilters()` for setup) and assert a normal query only sees one school's rows.
  - Adding a new `ITenantScoped` entity without explicitly setting `SchoolId` results in it being stamped with the current tenant's id after `SaveChangesAsync`.
  - `CreatedAt` is set on insert; `UpdatedAt` is set on a subsequent update and remains `null` until then.
  - The seed migration produces exactly one `Schools` row with the well-known seeded id.
  - `Repository<TEntity>.AddAsync` stages an entity without persisting it until `IUnitOfWork.SaveChangesAsync` is called — assert the row doesn't exist before the explicit save and does after.
  - `IUnitOfWork.BeginTransactionAsync` + `RollbackAsync` discards changes made through a repository in between (add an entity, roll back, assert it isn't there); `BeginTransactionAsync` + `CommitAsync` persists them.

## Boundaries

- **Always:** run `dotnet test` before considering a task in this spec done; follow the DI/layering rules in `.claude/rules/backend.md`; keep `BaseEntity`/`ITenantScoped` in `SchoolMgmt.Domain` with zero framework dependencies.
- **Ask first:** changing the `SchoolId` type (currently `Guid`) or the audit-field types, since every future entity spec builds on these; changing the seeded school's well-known id once other specs depend on it.
- **Never:** add a real tenant-onboarding UI or super-admin console as part of this spec (explicitly out of scope per the architecture decision); hand-set `SchoolId` in application/service code instead of relying on `SaveChangesAsync` automation; bypass the query filter (`IgnoreQueryFilters()`) anywhere outside test setup code, *or the two pre-authentication lookups documented in [specs/02-implement-auth.md](02-implement-auth.md)* (`IUserRepository.GetByEmailAsync`, `IRefreshTokenRepository.GetByTokenHashAsync` — login/refresh happen before a tenant is resolvable, so those two specific lookups are a deliberate, documented exception); call `SaveChanges`/`SaveChangesAsync` or any transaction method (`BeginTransaction`/`Commit`/`Rollback`) from inside a repository implementation — that belongs to `IUnitOfWork` only.

## Success Criteria

- `BaseEntity` and `ITenantScoped` exist in `SchoolMgmt.Domain` with no dependencies on other layers.
- `AppDbContext` in `SchoolMgmt.Infrastructure` applies a global query filter to every `ITenantScoped` entity automatically (reflection-based, not hand-written per entity).
- `ITenantProvider` is defined in `SchoolMgmt.Application` and implemented by a placeholder `StaticTenantProvider` in `SchoolMgmt.Infrastructure`, registered via `AddInfrastructure`.
- `SaveChangesAsync` automatically stamps `CreatedAt`, `UpdatedAt`, and `SchoolId` (for new `ITenantScoped` entities) without requiring application code to set them.
- One `School` row is seeded via the initial migration, consistent with the documented migration flow (`IDesignTimeDbContextFactory`, Docker `migrator` stage).
- `IRepository<TEntity>` and `IUnitOfWork` are defined in `SchoolMgmt.Application`; `Repository<TEntity>` and `UnitOfWork` implementations live in `SchoolMgmt.Infrastructure` and are registered via `AddInfrastructure` (open-generic registration for the repository).
- `Repository<TEntity>` contains no calls to `SaveChanges`/`SaveChangesAsync` or any transaction method — verified by the integration tests above (staged adds aren't persisted until `IUnitOfWork.SaveChangesAsync` runs).
- All integration tests listed under Testing Strategy pass against a real Postgres instance (Testcontainers).
- A developer can add a new tenant-scoped entity in a future spec by inheriting `BaseEntity` + implementing `ITenantScoped`, and add its repository by extending `IRepository<TEntity>`, with zero additional query-filter, persistence, or transaction wiring required.

## Open Questions

None remaining — the one open question (tenant resolution before Auth exists) was resolved: `ITenantProvider` abstraction now, with a placeholder implementation swapped out when the Auth spec (`specs/02-implement-auth.md`, expected next) lands.
