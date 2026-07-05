# Spec: Implement Subject Management

## Related docs & specs

- [docs/ideas/06-subject-management.md](../docs/ideas/06-subject-management.md) — idea doc: problem statement, flat-catalog rationale, code immutability decision, not-doing list
- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — source idea; subjects underpin teacher grade entry (future spec)
- [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md) — `BaseEntity`, `ITenantScoped`, `IRepository<TEntity>`, `IUnitOfWork`, `AppDbContext`; all used as-is
- [specs/05-implement-student-crud.md](05-implement-student-crud.md) — primary structural reference: paged list pattern, `PagedResult<T>`, search ILIKE backend addition, `IUnitOfWork` usage
- [specs/06-implement-teacher-crud.md](06-implement-teacher-crud.md) — secondary structural reference: `IsActive` soft-disable pattern, ConflictException via unique index, DI conventions
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller/Application-service pattern, GET-must-be-read-only, Repository/UnitOfWork rules, DI conventions

## Objective

Implement Admin-only CRUD for `Subject` — a flat, school-scoped catalog of subjects that grade-entry and teacher-assignment features will reference. A subject has a name, a human-entered immutable code, an optional description, and an `IsActive` flag. No teacher or section linkage in this spec.

**Out of scope for this spec:** academic-year scoping, grade-level hints, department/category grouping, teacher-subject assignment, bulk CSV import.

## Tech Stack

- .NET 8.0, C# — same solution as all prior specs
- EF Core (Npgsql) — one new table (`Subjects`)
- FluentValidation — request validators via the existing global `IAsyncActionFilter`
- xUnit + `WebApplicationFactory` + Testcontainers (Postgres) — same integration test setup

## Design

### Domain — `SchoolMgmt.Domain`

#### New: `Subject`

```csharp
// SchoolMgmt.Domain/Entities/Subject.cs
namespace SchoolMgmt.Domain.Entities;

public class Subject : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;  // e.g. "MATH", "SCI01" — immutable after create
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
```

`Code` is immutable once created. `SchoolId` is auto-stamped by `AppDbContext.SaveChangesAsync` via the `ITenantScoped` convention, same as all tenant entities.

### Application layer — `SchoolMgmt.Application`

#### `ISubjectRepository`

```csharp
// SchoolMgmt.Application/Subjects/ISubjectRepository.cs
namespace SchoolMgmt.Application.Subjects;

public interface ISubjectRepository : IRepository<Subject>
{
    Task<(List<Subject> Items, int TotalCount)> GetPagedAsync(
        bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default);
}
```

- `GetPagedAsync`: when `isActive` is null, defaults to active-only. `search` performs ILIKE against `Name` and `Code` (OR condition).
- `GetByIdAsync(Guid id)` is inherited from `IRepository<Subject>`.

#### DTOs

```csharp
// SchoolMgmt.Application/Subjects/Dtos/

public record SubjectSummaryDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record SubjectDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

// PagedResult<T> already exists in Application/Students/Dtos/ — reuse it

public record CreateSubjectRequest(
    string Name,
    string Code,
    string? Description
);

public record UpdateSubjectRequest(
    string Name,
    string? Description,
    bool IsActive
);
```

`Code` is NOT in `UpdateSubjectRequest` — it is immutable once assigned.

#### Validators (FluentValidation)

```csharp
// CreateSubjectRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
RuleFor(x => x.Code).NotEmpty().MaximumLength(20)
    .Matches(@"^[A-Za-z0-9_\-]+$").WithMessage("Code must contain only letters, numbers, hyphens, or underscores.");
RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);

// UpdateSubjectRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
```

Code uniqueness per school is enforced by the DB unique index — a `ConflictException` is thrown on violation, same as duplicate teacher email.

#### `SubjectService`

One service injecting `ISubjectRepository` and `IUnitOfWork`.

**Service method signatures:**

```
CreateSubjectAsync(CreateSubjectRequest, CancellationToken) → SubjectDto
GetSubjectsAsync(bool? isActive, string? search, int page, int pageSize, CancellationToken) → PagedResult<SubjectSummaryDto>
GetSubjectByIdAsync(Guid id, CancellationToken) → SubjectDto
UpdateSubjectAsync(Guid id, UpdateSubjectRequest, CancellationToken) → SubjectDto
```

**Algorithms:**

```
CreateSubjectAsync(CreateSubjectRequest, CancellationToken) → SubjectDto
  subject = new Subject
    { Name = request.Name, Code = request.Code, Description = request.Description, IsActive = true }
  await repository.AddAsync(subject, ct)
  await unitOfWork.SaveChangesAsync(ct)      // unique constraint throws ConflictException on duplicate code
  return ToDto(subject)

GetSubjectsAsync(bool? isActive, string? search, int page, int pageSize, CancellationToken) → PagedResult<SubjectSummaryDto>
  (items, total) = await repository.GetPagedAsync(isActive, search, page, pageSize, ct)
  return new PagedResult<SubjectSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize)

GetSubjectByIdAsync(Guid id, CancellationToken) → SubjectDto
  subject = await repository.GetByIdAsync(id, ct)
      ?? throw new NotFoundException("Subject not found.")
  return ToDto(subject)

UpdateSubjectAsync(Guid id, UpdateSubjectRequest, CancellationToken) → SubjectDto
  subject = await repository.GetByIdAsync(id, ct)
      ?? throw new NotFoundException("Subject not found.")
  subject.Name = request.Name
  subject.Description = request.Description
  subject.IsActive = request.IsActive
  repository.Update(subject)
  await unitOfWork.SaveChangesAsync(ct)
  return ToDto(subject)
  Note: Code is NOT updated — immutable.
```

#### `DependencyInjection.cs` (Application)

Append to `AddApplication`:

```csharp
services.AddScoped<SubjectService>();
```

### Infrastructure — `SchoolMgmt.Infrastructure`

#### `SubjectConfiguration`

```csharp
// Table: Subjects
builder.ToTable("Subjects");
builder.HasKey(x => x.Id);

builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();

builder.Property(x => x.Description).HasMaxLength(500);
builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
```

The unique index on `(SchoolId, Code)` is the enforcement point for code uniqueness per school.

#### `SubjectRepository`

```csharp
internal sealed class SubjectRepository(AppDbContext context)
    : Repository<Subject>(context), ISubjectRepository
{
    public async Task<(List<Subject> Items, int TotalCount)> GetPagedAsync(
        bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable();

        query = isActive.HasValue
            ? query.Where(s => s.IsActive == isActive.Value)
            : query.Where(s => s.IsActive);   // default: active only

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, pattern) ||
                EF.Functions.ILike(s.Code, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
```

The EF Core global query filter on `SchoolId` is applied automatically — the repository only sees the current tenant's subjects.

#### Modified: `AppDbContext`

Add:

```csharp
public DbSet<Subject> Subjects => Set<Subject>();
```

#### `DependencyInjection.cs` (Infrastructure)

Add to `AddInfrastructure`:

```csharp
services.AddScoped<ISubjectRepository, SubjectRepository>();
```

### WebApi — `SchoolMgmt.WebApi`

#### `SubjectsController`

All endpoints `[Authorize(Roles = "Admin")]`. All state-mutating operations are POST/PUT — no GET side effects. No DELETE endpoint.

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `POST` | `/api/subjects` | `CreateSubjectAsync` | 201 + `Location: /api/subjects/{id}` | 409 on duplicate code per school |
| `GET` | `/api/subjects` | `GetSubjectsAsync` | 200 — `PagedResult<SubjectSummaryDto>` | `isActive` (default: active only); `search` (optional ILIKE); `page` (default: 1); `pageSize` (default: 20, max: 100) |
| `GET` | `/api/subjects/{id}` | `GetSubjectByIdAsync` | 200 — `SubjectDto` | 404 if not found |
| `PUT` | `/api/subjects/{id}` | `UpdateSubjectAsync` | 200 — `SubjectDto` | 404 if not found; `code` is NOT updatable |

`pageSize` cap:

```csharp
pageSize = Math.Min(pageSize, 100);
```

## Project Structure

New and modified files introduced by this spec:

```
backend/
  SchoolMgmt.Domain/
    Entities/
      Subject.cs                                     # new

  SchoolMgmt.Application/
    Subjects/
      ISubjectRepository.cs                          # new
      SubjectService.cs                              # new
      Dtos/
        SubjectSummaryDto.cs                         # new
        SubjectDto.cs                                # new
        CreateSubjectRequest.cs                      # new
        UpdateSubjectRequest.cs                      # new
      Validators/
        CreateSubjectRequestValidator.cs             # new
        UpdateSubjectRequestValidator.cs             # new
    DependencyInjection.cs                           # add SubjectService

  SchoolMgmt.Infrastructure/
    Persistence/
      AppDbContext.cs                                # add DbSet<Subject>
      Configurations/
        SubjectConfiguration.cs                      # new
      Repositories/
        SubjectRepository.cs                         # new
      Migrations/
        <AddSubjects>                                # new — EF Core generated
    DependencyInjection.cs                           # add ISubjectRepository registration

  SchoolMgmt.WebApi/
    Controllers/
      SubjectsController.cs                          # new

tests/
  SchoolMgmt.IntegrationTests/
    SubjectsControllerTests.cs                       # new — real Postgres via Testcontainers
```

## Commands

```
Build:          dotnet build SchoolMgmt.slnx
Test:           dotnet test SchoolMgmt.slnx
Add migration:  dotnet ef migrations add AddSubjects --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
Apply locally:  dotnet ef database update --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

## Database Schema

### New: `Subjects`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | Surrogate primary key |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope (auto-stamped) |
| `Name` | `varchar` | 200 | — | NOT NULL | — | — | Display name, e.g. "Mathematics" |
| `Code` | `varchar` | 20 | — | NOT NULL | — | UNIQUE with `SchoolId` | Short human-readable code, e.g. "MATH"; immutable |
| `Description` | `varchar` | 500 | — | NULL | — | — | Optional long-form description |
| `IsActive` | `boolean` | — | `true` | NOT NULL | — | — | Soft-disable retired subjects |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | Auto-stamped by `AppDbContext` |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | Auto-stamped on modification |

## Testing Strategy

No domain unit tests — `Subject` has no domain behavior (no guards, no invariants).

### Integration tests (`SchoolMgmt.IntegrationTests`)

Against real Postgres (Testcontainers), authenticated as the demo Admin:

**Create:**
- `POST /api/subjects` with valid data → 201; response body contains `id`, `code`, `isActive = true`
- `POST /api/subjects` with missing `name` → 400
- `POST /api/subjects` with `code` exceeding 20 chars → 400
- `POST /api/subjects` with code containing spaces → 400 (regex validation)
- `POST /api/subjects` with a duplicate code (same school) → 409
- `POST /api/subjects` with `description` omitted → 201; `description` is null in response

**Read:**
- `GET /api/subjects` (no params) → returns only active subjects
- `GET /api/subjects?isActive=false` → returns only inactive subjects
- `GET /api/subjects?isActive=true` → returns only active subjects
- `GET /api/subjects?search=math` → returns subjects matching ILIKE on name or code
- `GET /api/subjects?page=1&pageSize=5` → returns up to 5; `totalCount` and `page` fields present
- `GET /api/subjects?pageSize=200` → clamped to 100
- `GET /api/subjects/{id}` → 200 with full `SubjectDto`
- `GET /api/subjects/{unknownId}` → 404

**Update:**
- `PUT /api/subjects/{id}` with updated name → 200; response reflects new name; `updatedAt` is set
- `PUT /api/subjects/{id}` with `isActive = false` → 200; subject no longer appears in default `GET /api/subjects`
- `PUT /api/subjects/{id}` — verify `code` in response is unchanged from create
- `PUT /api/subjects/{unknownId}` → 404

**No delete:**
- `DELETE /api/subjects/{id}` → 405

**Auth:**
- Unauthenticated request to any endpoint → 401
- Request with Teacher role → 403

## Boundaries

- **Always:** call `dotnet test SchoolMgmt.slnx` before considering any task done; follow the DI/layering rules in `.claude/rules/backend.md`; keep GET endpoints side-effect-free; never update `Code` in `UpdateSubjectAsync`.
- **Ask first:** exposing code update via this API (currently immutable by design — requires discussion before relaxing); adding academic-year scoping (structural change, new FK on every downstream reference); adding a `SubjectCategory` entity.
- **Never:** hard-delete a subject; call `SaveChangesAsync` or transaction methods from inside `SubjectRepository`; put `Code` in `UpdateSubjectRequest`.

## Success Criteria

- `POST /api/subjects` returns 201 with the supplied `code` preserved exactly — integration test passes.
- `POST /api/subjects` with a duplicate code (same school) returns 409 — integration test confirms.
- `GET /api/subjects` (no params) returns only active subjects — integration test verifies after one subject is deactivated.
- `GET /api/subjects?search=mat` returns subjects whose name or code contains "mat" case-insensitively — integration test confirms.
- `PUT /api/subjects/{id}` — `code` in response matches the original, not any request body field — integration test confirms immutability.
- `DELETE /api/subjects/{id}` returns 405.
- All `GET /api/subjects/{id}` for unknown IDs return 404.
- All endpoints return 401 for unauthenticated requests and 403 for Teacher-role requests.
- All tests in `dotnet test SchoolMgmt.slnx` pass.

## Open Questions

None — all design decisions resolved in the idea doc and the ideation session.
