# Spec: Implement Academic Year / Term Configuration

## Related docs & specs

- [docs/ideas/01-academic-year-term-configuration.md](../docs/ideas/01-academic-year-term-configuration.md) — idea doc: problem statement, direction (Option B domain guard, auto-scaffold semesters, explicit admin transitions), scope and not-doing list
- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — source idea: academic year/term setup is Admin-owned, cross-cutting, foundational
- [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md) — provides `BaseEntity`, `ITenantScoped`, `IRepository<TEntity>`, `IUnitOfWork`, `DomainException` (defined here, following the same pattern), `AppDbContext`, and `IDesignTimeDbContextFactory` — all used without modification
- [specs/02-implement-auth.md](02-implement-auth.md) — provides `[Authorize(Roles = "Admin")]` and the RBAC infrastructure; all endpoints in this spec are Admin-only
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller/Application-service pattern, DI conventions, GET-must-be-read-only, Repository/UnitOfWork rules

## Objective

Implement a two-level calendar structure — `AcademicYear` containing two auto-scaffolded `Semester` entities — that every future module (fees, grades, attendance, enrollment) can anchor to. Admins can create years, edit semester dates, set one year and one semester as "current", and archive completed years. Archived years are protected by a domain guard (`AcademicYear.EnsureNotArchived()`) that all downstream services will call before any write — establishing the enforcement contract now, once, so future modules don't carry a per-service "remember to check" burden.

**Out of scope for this spec:** any downstream module enforcing the guard (FeeService, GradeService, etc. — those are built on top of this spec when their own specs land); semester deletion; custom term counts; date-based automatic current-year inference; any frontend UI.

## Tech Stack

- .NET 8.0, C# — same solution structure as specs #1 and #2
- EF Core (Npgsql) — two new tables (`AcademicYears`, `Semesters`), `DateOnly` mapped to PostgreSQL `date`
- FluentValidation — request validators registered via the existing global `IAsyncActionFilter` (already wired from spec #2; no new infra needed)
- xUnit + `WebApplicationFactory` + Testcontainers (Postgres) — same test setup as prior specs

## Design

### Domain (`SchoolMgmt.Domain`)

#### `DomainException`

A shared base exception for all domain rule violations. Defined here (first spec to need it); reused by all future domain entities. Mapped to HTTP 400 in WebApi via a new `DomainExceptionFilter`.

```csharp
namespace SchoolMgmt.Domain.Common;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
```

#### `AcademicYearStatus` enum

```csharp
namespace SchoolMgmt.Domain.Enums;

public enum AcademicYearStatus { Active, Archived }
```

Stored as string in the database (not int) — same convention as `UserRole` from spec #2. Prevents silent corruption if the enum is reordered.

#### `AcademicYear`

```csharp
namespace SchoolMgmt.Domain.Entities;

public class AcademicYear : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;         // e.g. "2024-2025"
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AcademicYearStatus Status { get; private set; } = AcademicYearStatus.Active;
    public bool IsCurrent { get; private set; }

    private readonly List<Semester> _semesters = [];
    public IReadOnlyList<Semester> Semesters => _semesters.AsReadOnly();

    // Called by AppDbContext only — private setters prevent external mutation
    public void SetCurrent(bool value) => IsCurrent = value;
    public void Archive()
    {
        if (IsCurrent)
            throw new DomainException("Cannot archive the current academic year. Set a different year as current first.");
        Status = AcademicYearStatus.Archived;
    }

    // Domain guard — called by every downstream Application service before any write
    public void EnsureNotArchived()
    {
        if (Status == AcademicYearStatus.Archived)
            throw new DomainException("Cannot modify data in an archived academic year.");
    }

    // Factory — always called instead of the constructor; enforces the 2-semester invariant
    public static AcademicYear Create(string name, DateOnly startDate, DateOnly endDate)
    {
        var year = new AcademicYear
        {
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
        };
        year._semesters.Add(new Semester { Name = "Semester 1", AcademicYear = year });
        year._semesters.Add(new Semester { Name = "Semester 2", AcademicYear = year });
        return year;
    }
}
```

**Design notes:**
- `SetCurrent`/`Archive` are the only mutation paths — no public `IsCurrent` setter, preventing ad-hoc flag-flipping outside the Application service.
- `Archive()` enforces the "cannot archive the current year" rule at the domain level — even if the service forgets to check, the entity refuses.
- `EnsureNotArchived()` is the shared guard that downstream services call before every write — the entire point of Option B from the idea doc.
- `_semesters` is a backing field; EF Core maps it via the `Semesters` navigation using `HasField("_semesters")` in `AcademicYearConfiguration`.
- `Create()` is the only way to produce a well-formed `AcademicYear` with its two semesters — the parameterless constructor stays private for EF Core via `UsePropertyAccessMode`.

#### `Semester`

```csharp
namespace SchoolMgmt.Domain.Entities;

public class Semester : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public string Name { get; set; } = string.Empty;         // "Semester 1" / "Semester 2"
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; private set; }

    public void SetCurrent(bool value) => IsCurrent = value;
}
```

`Semester` is also `ITenantScoped` (carries `SchoolId`) — consistent with the architecture rule that every tenant-scoped entity carries the column directly. `SaveChangesAsync` auto-stamps it from the tenant context.

### Application layer (`SchoolMgmt.Application`)

#### `IAcademicYearRepository`

```csharp
namespace SchoolMgmt.Application.AcademicYears;

public interface IAcademicYearRepository : IRepository<AcademicYear>
{
    Task<List<AcademicYear>> GetAllWithSemestersAsync(CancellationToken ct = default);
    Task<AcademicYear?> GetWithSemestersAsync(Guid id, CancellationToken ct = default);
    Task<AcademicYear?> GetCurrentAsync(CancellationToken ct = default);
    Task<Semester?> GetCurrentSemesterAsync(CancellationToken ct = default);
    Task<Semester?> GetSemesterByIdAsync(Guid semesterId, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
}
```

#### DTOs

```csharp
namespace SchoolMgmt.Application.AcademicYears;

public record SemesterDto(
    Guid Id,
    Guid AcademicYearId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent);

public record AcademicYearDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    bool IsCurrent,
    List<SemesterDto> Semesters);

public record CreateAcademicYearRequest(string Name, DateOnly StartDate, DateOnly EndDate);

public record UpdateSemesterRequest(string Name, DateOnly StartDate, DateOnly EndDate);
```

#### Validators (FluentValidation)

```csharp
// CreateAcademicYearRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
RuleFor(x => x.StartDate).NotEmpty();
RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate)
    .WithMessage("End date must be after start date.");

// UpdateSemesterRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
RuleFor(x => x.StartDate).NotEmpty();
RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate)
    .WithMessage("End date must be after start date.");
```

#### `AcademicYearService`

One service per feature area per the backend rules. All methods follow the thin-service pattern: load, call domain method, save.

```
CreateAcademicYearAsync(CreateAcademicYearRequest, CancellationToken) → AcademicYearDto
  - Validate name uniqueness (IAcademicYearRepository.NameExistsAsync) → 409 if duplicate
  - Call AcademicYear.Create(name, startDate, endDate) — auto-scaffolds Semester 1 + 2
  - repository.AddAsync(year)
  - unitOfWork.SaveChangesAsync()
  - Return AcademicYearDto

GetAllAcademicYearsAsync(CancellationToken) → List<AcademicYearDto>

GetAcademicYearByIdAsync(Guid id, CancellationToken) → AcademicYearDto
  - 404 if not found

UpdateSemesterAsync(Guid semesterId, UpdateSemesterRequest, CancellationToken) → SemesterDto
  - Load semester (GetSemesterByIdAsync) → 404 if not found
  - Load parent year (GetByIdAsync) → call year.EnsureNotArchived()
  - Update semester Name, StartDate, EndDate
  - unitOfWork.SaveChangesAsync()

SetCurrentYearAsync(Guid yearId, CancellationToken) → void
  - Load year with semesters → 404 if not found
  - If year.Status == Archived → throw DomainException
  - unitOfWork.BeginTransactionAsync()
  - Unset previous current year: load via GetCurrentAsync, call SetCurrent(false) if not null
  - Unset previous current semester: load via GetCurrentSemesterAsync, call SetCurrent(false) if not null
  - year.SetCurrent(true)
  - Auto-set Semester 1 of the new year as current:
      year.Semesters.First(s => s.Name == "Semester 1").SetCurrent(true)
  - unitOfWork.SaveChangesAsync()
  - unitOfWork.CommitAsync()

SetCurrentSemesterAsync(Guid semesterId, CancellationToken) → void
  - Load semester → 404 if not found
  - Load current year (GetCurrentAsync) → if none, throw DomainException("No current academic year is set.")
  - If semester.AcademicYearId != currentYear.Id → throw DomainException("Semester does not belong to the current academic year.")
  - unitOfWork.BeginTransactionAsync()
  - Unset previous current semester (GetCurrentSemesterAsync → SetCurrent(false))
  - semester.SetCurrent(true)
  - unitOfWork.SaveChangesAsync()
  - unitOfWork.CommitAsync()

ArchiveAcademicYearAsync(Guid yearId, CancellationToken) → void
  - Load year → 404 if not found
  - year.Archive()  ← throws DomainException if IsCurrent (domain rule enforces this)
  - unitOfWork.SaveChangesAsync()
```

**Mapping convention:** service methods map entities → DTOs inline (no AutoMapper). For a feature this size, explicit projection is readable and requires no additional dependency. Use a private static `ToDto(AcademicYear)` helper method in the service to avoid duplication across methods.

#### `DependencyInjection.cs` (Application)

```csharp
// SchoolMgmt.Application/DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddScoped<AcademicYearService>();
    // FluentValidation validators are registered here too (once, for all feature validators)
    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    return services;
}
```

If `AddApplication` already exists from a prior spec, append the `AcademicYearService` registration; the validator registration may already be present.

### Infrastructure (`SchoolMgmt.Infrastructure`)

#### `AcademicYearConfiguration`

```csharp
// Table: AcademicYears
builder.ToTable("AcademicYears");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
builder.Property(x => x.StartDate).HasColumnType("date");
builder.Property(x => x.EndDate).HasColumnType("date");
builder.Property(x => x.Status)
    .HasConversion<string>()
    .HasMaxLength(20)
    .IsRequired();
builder.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();

// Backing field for Semesters navigation
builder.Navigation(x => x.Semesters).HasField("_semesters");
```

#### `SemesterConfiguration`

```csharp
// Table: Semesters
builder.ToTable("Semesters");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
builder.Property(x => x.StartDate).HasColumnType("date");
builder.Property(x => x.EndDate).HasColumnType("date");

// FK: ON DELETE RESTRICT — years are never hard-deleted, only archived
builder.HasOne(x => x.AcademicYear)
    .WithMany("_semesters")
    .HasForeignKey(x => x.AcademicYearId)
    .OnDelete(DeleteBehavior.Restrict);
```

#### `AcademicYearRepository`

```csharp
internal sealed class AcademicYearRepository(AppDbContext context)
    : Repository<AcademicYear>(context), IAcademicYearRepository
{
    public Task<List<AcademicYear>> GetAllWithSemestersAsync(CancellationToken ct = default) =>
        DbSet.Include(y => y.Semesters).OrderByDescending(y => y.StartDate).ToListAsync(ct);

    public Task<AcademicYear?> GetWithSemestersAsync(Guid id, CancellationToken ct = default) =>
        DbSet.Include(y => y.Semesters).FirstOrDefaultAsync(y => y.Id == id, ct);

    public Task<AcademicYear?> GetCurrentAsync(CancellationToken ct = default) =>
        DbSet.Include(y => y.Semesters).FirstOrDefaultAsync(y => y.IsCurrent, ct);

    public Task<Semester?> GetCurrentSemesterAsync(CancellationToken ct = default) =>
        context.Set<Semester>().FirstOrDefaultAsync(s => s.IsCurrent, ct);

    public Task<Semester?> GetSemesterByIdAsync(Guid semesterId, CancellationToken ct = default) =>
        context.Set<Semester>().FirstOrDefaultAsync(s => s.Id == semesterId, ct);

    public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) =>
        DbSet.AnyAsync(y => y.Name == name, ct);
}
```

Note: `context.Set<Semester>()` is used directly — `Semester` does not get its own `IAcademicYearRepository`-level interface since it's always accessed through the year; no standalone `ISemesterRepository` is introduced.

#### `DependencyInjection.cs` (Infrastructure)

Add to `AddInfrastructure`:

```csharp
services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
```

### WebApi (`SchoolMgmt.WebApi`)

#### `DomainExceptionFilter`

Maps `DomainException` → HTTP 400 Bad Request. Registered as a global filter in `Program.cs`.

```csharp
public class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DomainException) return;
        context.Result = new BadRequestObjectResult(new { error = context.Exception.Message });
        context.ExceptionHandled = true;
    }
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.Add<DomainExceptionFilter>();
    // existing filters (ValidationFilter, LoggingFilter) stay here
});
```

#### `AcademicYearsController`

All endpoints require `[Authorize(Roles = "Admin")]`. All state-mutating operations are POST/PUT — no GET side effects.

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `GET` | `/api/academic-years` | `GetAllAcademicYearsAsync` | 200 | — |
| `GET` | `/api/academic-years/{id}` | `GetAcademicYearByIdAsync` | 200 | 404 if not found |
| `POST` | `/api/academic-years` | `CreateAcademicYearAsync` | 201 + Location header | 409 if name duplicate |
| `PUT` | `/api/academic-years/{yearId}/semesters/{semesterId}` | `UpdateSemesterAsync` | 200 | 400 if year archived |
| `POST` | `/api/academic-years/{id}/set-current` | `SetCurrentYearAsync` | 204 | 400 if archived |
| `POST` | `/api/academic-years/{yearId}/semesters/{semesterId}/set-current` | `SetCurrentSemesterAsync` | 204 | 400 if wrong year |
| `POST` | `/api/academic-years/{id}/archive` | `ArchiveAcademicYearAsync` | 204 | 400 if currently active |

The `set-current` and `archive` routes are POST (not PATCH/PUT) because they convey a discrete lifecycle transition, not a property update — and POST is unambiguous for state-machine transitions.

## Project Structure

New files introduced by this spec:

```
backend/
  SchoolMgmt.Domain/
    Common/
      DomainException.cs               # new — shared by all future domain guards
    Entities/
      AcademicYear.cs                  # new
      Semester.cs                      # new
    Enums/
      AcademicYearStatus.cs            # new

  SchoolMgmt.Application/
    AcademicYears/
      IAcademicYearRepository.cs       # new
      AcademicYearService.cs           # new
      Dtos/
        AcademicYearDto.cs             # new
        SemesterDto.cs                 # new
        CreateAcademicYearRequest.cs   # new
        UpdateSemesterRequest.cs       # new
      Validators/
        CreateAcademicYearRequestValidator.cs  # new
        UpdateSemesterRequestValidator.cs      # new
    DependencyInjection.cs             # add AcademicYearService + validators registration

  SchoolMgmt.Infrastructure/
    Persistence/
      Configurations/
        AcademicYearConfiguration.cs   # new
        SemesterConfiguration.cs       # new
      Repositories/
        AcademicYearRepository.cs      # new
      Migrations/
        <AddAcademicYears migration>   # new — generated by EF Core tooling
    DependencyInjection.cs             # add IAcademicYearRepository registration

  SchoolMgmt.WebApi/
    Controllers/
      AcademicYearsController.cs       # new
    Filters/
      DomainExceptionFilter.cs         # new
    Program.cs                         # add DomainExceptionFilter to global filters

tests/
  SchoolMgmt.Domain.Tests/             # new project (unit — no DB, no fakes needed for pure logic)
    AcademicYearTests.cs
  SchoolMgmt.IntegrationTests/
    AcademicYearsControllerTests.cs    # new test class in existing integration test project
```

## Commands

```
Build:          dotnet build SchoolMgmt.slnx
Test:           dotnet test SchoolMgmt.slnx
Add migration:  dotnet ef migrations add AddAcademicYears --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
Apply locally:  dotnet ef database update --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

## Database Schema Additions

### `AcademicYears`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | — |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope |
| `Name` | `varchar` | 100 | — | NOT NULL | — | UNIQUE with `SchoolId` | e.g. "2024-2025" |
| `StartDate` | `date` | — | — | NOT NULL | — | — | — |
| `EndDate` | `date` | — | — | NOT NULL | — | — | — |
| `Status` | `varchar` | 20 | `'Active'` | NOT NULL | — | — | `Active` or `Archived` |
| `IsCurrent` | `bool` | — | `false` | NOT NULL | — | — | At most one per school |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

### `Semesters`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | — |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope |
| `AcademicYearId` | `uuid` | — | — | NOT NULL | FK → `AcademicYears.Id` | ON DELETE RESTRICT | — |
| `Name` | `varchar` | 100 | — | NOT NULL | — | — | "Semester 1" / "Semester 2" |
| `StartDate` | `date` | — | — | NOT NULL | — | — | — |
| `EndDate` | `date` | — | — | NOT NULL | — | — | — |
| `IsCurrent` | `bool` | — | `false` | NOT NULL | — | — | At most one per school |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

`ON DELETE RESTRICT` on `AcademicYearId` — years are never hard-deleted (only archived), so cascade deletion is not needed; RESTRICT guards against any accidental delete path.

## Testing Strategy

### Unit tests (`SchoolMgmt.Domain.Tests` — new project, no DB, no fakes)

Pure domain logic tests:

- `AcademicYear.Create` produces exactly 2 semesters named "Semester 1" and "Semester 2"
- `AcademicYear.EnsureNotArchived` throws `DomainException` when `Status == Archived`
- `AcademicYear.EnsureNotArchived` does not throw when `Status == Active`
- `AcademicYear.Archive` throws `DomainException` when `IsCurrent == true`
- `AcademicYear.Archive` sets `Status = Archived` when `IsCurrent == false`

### Integration tests (`SchoolMgmt.IntegrationTests`)

Against real Postgres (Testcontainers), authenticated as demo Admin:

- `POST /api/academic-years` — creates year, response body contains 2 semesters; 201 returned
- `POST /api/academic-years` with duplicate name — returns 409
- `GET /api/academic-years` — returns created year with its semesters
- `GET /api/academic-years/{id}` — returns 404 for unknown id
- `PUT /api/academic-years/{yearId}/semesters/{semesterId}` — updates semester name/dates; returns 200
- `POST /api/academic-years/{id}/set-current` — sets year as current; re-fetching the year shows `isCurrent: true`; the previously-current year (if any) shows `isCurrent: false`; Semester 1 of the new year shows `isCurrent: true`
- `POST /api/academic-years/{yearId}/semesters/{semesterId}/set-current` — overrides current semester to Semester 2; verifying Semester 1 is no longer current
- `POST /api/academic-years/{yearId}/semesters/{semesterId}/set-current` with a semester from a non-current year — returns 400
- `POST /api/academic-years/{id}/archive` — archives a non-current year; subsequent `PUT` on its semester returns 400
- `POST /api/academic-years/{id}/archive` on the current year — returns 400
- Unauthenticated request to any endpoint — returns 401
- Request with Teacher role — returns 403

## Boundaries

- **Always:** call `year.EnsureNotArchived()` in any downstream service method that writes data scoped to an academic year or semester — this is the enforcement contract established by this spec for all future modules. Run `dotnet test` before considering any task complete.
- **Ask first:** changing `DateOnly` to `DateTimeOffset` for start/end dates (would require a migration change and affects all downstream modules); changing the "two semesters" invariant in `AcademicYear.Create` (would break the `SetCurrentYearAsync` auto-set-Semester-1 logic).
- **Never:** skip the `EnsureNotArchived()` guard in a downstream service and write directly to an archived year; add a `GET` endpoint that mutates state (SameSite=Lax CSRF rule from [.claude/rules/backend.md](../.claude/rules/backend.md)); delete an `AcademicYear` or `Semester` record — archiving is the only lifecycle end-state; let the `IsCurrent` flag get set outside of `SetCurrent(bool)` / `Archive()` on the entity.

## Success Criteria

- `AcademicYear.Create` always produces exactly 2 semesters ("Semester 1", "Semester 2") — verified by domain unit test.
- `AcademicYear.EnsureNotArchived()` throws `DomainException` when archived — domain unit test passes.
- `POST /api/academic-years` creates a year with 2 auto-scaffolded semesters — integration test passes.
- `POST /api/academic-years/{id}/set-current` atomically unsets the previous current year/semester and sets the new year + its Semester 1 as current — verified by integration test asserting the before/after state.
- `PUT` on a semester of an archived year returns HTTP 400 — integration test passes.
- `POST /api/academic-years/{id}/archive` on the current year returns HTTP 400 — domain rule enforced at entity level, surfaced via `DomainExceptionFilter`.
- All new endpoints return 401 for unauthenticated requests and 403 for non-Admin roles.
- `DomainException` is mapped to HTTP 400 globally via `DomainExceptionFilter` — one registration, works for all future domain guards without additional wiring.
- All tests in `dotnet test SchoolMgmt.slnx` pass.

## Open Questions

None — all design decisions resolved in the idea doc and refined here.
