# Spec: Implement Student CRUD

## Related docs & specs

- [docs/ideas/03-student-crud.md](../docs/ideas/03-student-crud.md) — idea doc: problem statement, StudentCode rationale, guardian inline vs. separate-table decision, scope and not-doing list
- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — source idea: Student is the central operational record in the system; this slice unblocks attendance, grades, fees, and the parent portal
- [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md) — provides `BaseEntity`, `ITenantScoped`, `IRepository<TEntity>`, `IUnitOfWork`, `AppDbContext`; all used without modification except the `IUnitOfWork.Detach<T>` addition described below
- [specs/02-implement-auth.md](02-implement-auth.md) — provides `[Authorize(Roles = "Admin")]` and RBAC infrastructure; all endpoints in this spec are Admin-only
- [specs/04-implement-class-section-structure.md](04-implement-class-section-structure.md) — `Section` is the structural anchor for class assignment (a future slice); this spec does **not** add a `SectionId` FK to `Student` — that is deliberately deferred
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller/Application-service pattern, DI conventions, GET-must-be-read-only, Repository/UnitOfWork rules

## Objective

Implement Admin-only CRUD for `Student` — the central operational record in the system. A student carries two identifiers: a `Guid Id` primary key (globally unique) and a `StudentCode` (human-readable, tenant-scoped unique, immutable). Students are never hard-deleted; an `EnrollmentStatus` enum handles all lifecycle transitions. Guardian contact info is stored inline on the student row. Class/section assignment is explicitly out of scope for this spec.

**Out of scope for this spec:** class/section assignment (students to sections), staff CRUD, parent-portal auth link (one guardian → multiple children), bulk CSV import, student photo.

## Tech Stack

- .NET 8.0, C# — same solution structure as specs #1–#4
- EF Core (Npgsql) — one new table (`Students`), `DateOnly` mapped to PostgreSQL `date`
- FluentValidation — request validators registered via the existing global `IAsyncActionFilter`
- xUnit + `WebApplicationFactory` + Testcontainers (Postgres) — same test setup as prior specs

## Design

### Domain (`SchoolMgmt.Domain`)

#### Enums

Two new enums in `SchoolMgmt.Domain/Enums/`:

```csharp
// SchoolMgmt.Domain/Enums/Gender.cs
namespace SchoolMgmt.Domain.Enums;
public enum Gender { Male, Female, Other }

// SchoolMgmt.Domain/Enums/EnrollmentStatus.cs
namespace SchoolMgmt.Domain.Enums;
public enum EnrollmentStatus { Active, Transferred, Graduated, Dropped }
```

#### `Student`

```csharp
// SchoolMgmt.Domain/Entities/Student.cs
namespace SchoolMgmt.Domain.Entities;

public class Student : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public DateOnly EnrollmentDate { get; set; }
    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Active;
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianEmail { get; set; }
}
```

**Design notes:**
- No factory method — `Student` has no multi-field invariant requiring controlled construction. Object initializer is correct.
- `StudentCode` is immutable by convention (no endpoint exposes it for update); the DB unique constraint and the spec boundary enforce this.
- `SchoolId` is auto-stamped by `AppDbContext.SaveChangesAsync` from `ITenantProvider.CurrentSchoolId` when the entity is first saved, exactly as with all other `ITenantScoped` entities.

### Application layer (`SchoolMgmt.Application`)

#### `IUnitOfWork` — Detach extension

Add one method to the existing `IUnitOfWork` interface to support the StudentCode-generation retry loop (see `StudentService.CreateStudentAsync` below). This is the only change to an existing interface in this spec.

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    void Detach<T>(T entity) where T : class;   // ← new
}
```

`EfUnitOfWork` implements it as:

```csharp
public void Detach<T>(T entity) where T : class =>
    context.Entry(entity).State = EntityState.Detached;
```

**Why:** `UnitOfWork.SaveChangesAsync` translates any PostgreSQL `23505` unique violation to `ConflictException`. On a `ConflictException` during `CreateStudentAsync` (signalling a rare concurrent StudentCode collision), the service must detach the failed entity from the EF change tracker before constructing a new one with the next code — otherwise the old `Added` entity is still tracked and re-attempted on the next `SaveChangesAsync`. `Detach<T>` keeps the detach call in the Application layer without leaking `DbContext` or `EntityState` through the layer boundary.

#### `IStudentRepository`

```csharp
// SchoolMgmt.Application/Students/IStudentRepository.cs
namespace SchoolMgmt.Application.Students;

public interface IStudentRepository : IRepository<Student>
{
    Task<(List<Student> Items, int TotalCount)> GetPagedAsync(
        EnrollmentStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task<string> GetNextStudentCodeAsync(int enrollmentYear, CancellationToken ct = default);
}
```

`GetByIdAsync(Guid id, CancellationToken)` is inherited from `IRepository<Student>` — not re-declared here.

#### DTOs

```csharp
// SchoolMgmt.Application/Students/Dtos/

public record StudentSummaryDto(
    Guid Id,
    string StudentCode,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,
    DateOnly EnrollmentDate,
    string EnrollmentStatus
);

public record StudentDto(
    Guid Id,
    string StudentCode,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,
    DateOnly EnrollmentDate,
    string EnrollmentStatus,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public record CreateStudentRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,             // "Male" | "Female" | "Other"
    DateOnly EnrollmentDate,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail
);

public record UpdateStudentRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,             // "Male" | "Female" | "Other"
    DateOnly EnrollmentDate,
    string EnrollmentStatus,   // "Active" | "Transferred" | "Graduated" | "Dropped"
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail
);
```

`Gender` and `EnrollmentStatus` in request DTOs are received as strings. The validator rejects invalid values via `IsEnumName`; the service parses them with `Enum.Parse<T>` (case-insensitive). This avoids configuring `JsonStringEnumConverter` globally and keeps the API explicit.

#### Validators (FluentValidation)

```csharp
// CreateStudentRequestValidator
RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
RuleFor(x => x.Gender).NotEmpty().IsEnumName(typeof(Gender), caseSensitive: false);
RuleFor(x => x.EnrollmentDate).NotEmpty();
RuleFor(x => x.GuardianName).MaximumLength(200).When(x => x.GuardianName is not null);
RuleFor(x => x.GuardianPhone).MaximumLength(20).When(x => x.GuardianPhone is not null);
RuleFor(x => x.GuardianEmail)
    .MaximumLength(256).EmailAddress()
    .When(x => !string.IsNullOrEmpty(x.GuardianEmail));

// UpdateStudentRequestValidator
RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
RuleFor(x => x.Gender).NotEmpty().IsEnumName(typeof(Gender), caseSensitive: false);
RuleFor(x => x.EnrollmentDate).NotEmpty();
RuleFor(x => x.EnrollmentStatus).NotEmpty().IsEnumName(typeof(EnrollmentStatus), caseSensitive: false);
RuleFor(x => x.GuardianName).MaximumLength(200).When(x => x.GuardianName is not null);
RuleFor(x => x.GuardianPhone).MaximumLength(20).When(x => x.GuardianPhone is not null);
RuleFor(x => x.GuardianEmail)
    .MaximumLength(256).EmailAddress()
    .When(x => !string.IsNullOrEmpty(x.GuardianEmail));
```

#### `StudentService`

One service for all student operations. Private `ToDto`/`ToSummaryDto` helpers; no AutoMapper.

**StudentCode generation algorithm:**

`GetNextStudentCodeAsync(enrollmentYear)` in the repository queries `MAX(StudentCode)` for the tenant's students whose code starts with `"{enrollmentYear}-"`. If no students exist for that year, returns `"{enrollmentYear}-000001"`. Otherwise, parses the 6-digit sequence from the MAX result, increments by 1, and returns `"{enrollmentYear}-{next:D6}"`.

The service wraps the insert in a retry loop (max 3 attempts) to handle extremely rare concurrent collisions — two admins simultaneously creating students in the same year with the same MAX result. On each `ConflictException` catch (before the final attempt), the failed entity is detached from the EF change tracker via `unitOfWork.Detach(student)` before constructing a fresh entity with the next code.

```
CreateStudentAsync(CreateStudentRequest, CancellationToken) → StudentDto
  for attempt in 0..2:
    - code = await repository.GetNextStudentCodeAsync(request.EnrollmentDate.Year, ct)
    - student = new Student { StudentCode = code, FirstName = ..., LastName = ...,
        DateOfBirth = ..., Gender = Enum.Parse<Gender>(request.Gender, true),
        EnrollmentDate = ..., EnrollmentStatus = EnrollmentStatus.Active,
        GuardianName = ..., GuardianPhone = ..., GuardianEmail = ... }
    - await repository.AddAsync(student, ct)
    - try: await unitOfWork.SaveChangesAsync(ct); return ToDto(student)
    - catch ConflictException when attempt < 2: unitOfWork.Detach(student); continue
  throw DomainException("Unable to assign a student code. Please try again.")

GetStudentsAsync(EnrollmentStatus? status, int page, int pageSize, CancellationToken) → PagedResult<StudentSummaryDto>
  - (items, total) = await repository.GetPagedAsync(status, page, pageSize, ct)
  - return new PagedResult<StudentSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize)

GetStudentByIdAsync(Guid id, CancellationToken) → StudentDto
  - student = await repository.GetByIdAsync(id, ct)
      ?? throw new NotFoundException("Student not found.")
  - return ToDto(student)

UpdateStudentAsync(Guid id, UpdateStudentRequest, CancellationToken) → StudentDto
  - student = await repository.GetByIdAsync(id, ct)
      ?? throw new NotFoundException("Student not found.")
  - student.FirstName = request.FirstName
  - student.LastName = request.LastName
  - student.DateOfBirth = request.DateOfBirth
  - student.Gender = Enum.Parse<Gender>(request.Gender, true)
  - student.EnrollmentDate = request.EnrollmentDate
  - student.EnrollmentStatus = Enum.Parse<EnrollmentStatus>(request.EnrollmentStatus, true)
  - student.GuardianName = request.GuardianName
  - student.GuardianPhone = request.GuardianPhone
  - student.GuardianEmail = request.GuardianEmail
  - repository.Update(student)
  - await unitOfWork.SaveChangesAsync(ct)
  - return ToDto(student)
  Note: StudentCode is NOT updated — it is immutable once assigned.
```

#### `DependencyInjection.cs` (Application)

Append to the existing `AddApplication` method:

```csharp
services.AddScoped<StudentService>();
```

Validators are registered via the existing `services.AddValidatorsFromAssembly(...)` call.

### Infrastructure (`SchoolMgmt.Infrastructure`)

#### `EfUnitOfWork` — Detach implementation

Add to the existing `UnitOfWork` class (which already implements the other `IUnitOfWork` methods):

```csharp
public void Detach<T>(T entity) where T : class =>
    context.Entry(entity).State = EntityState.Detached;
```

#### `StudentConfiguration`

```csharp
// Table: Students
builder.ToTable("Students");
builder.HasKey(x => x.Id);

builder.Property(x => x.StudentCode).IsRequired().HasMaxLength(20);
builder.HasIndex(x => new { x.SchoolId, x.StudentCode }).IsUnique();

builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
builder.Property(x => x.DateOfBirth).IsRequired().HasColumnType("date");
builder.Property(x => x.Gender).IsRequired().HasConversion<string>().HasMaxLength(20);
builder.Property(x => x.EnrollmentDate).IsRequired().HasColumnType("date");
builder.Property(x => x.EnrollmentStatus).IsRequired().HasConversion<string>().HasMaxLength(20);

builder.Property(x => x.GuardianName).HasMaxLength(200);
builder.Property(x => x.GuardianPhone).HasMaxLength(20);
builder.Property(x => x.GuardianEmail).HasMaxLength(256);
```

`HasConversion<string>()` stores enums as their string names (`"Male"`, `"Active"`, etc.), consistent with how `UserRole` is stored in the `Users` table.

#### `StudentRepository`

```csharp
internal sealed class StudentRepository(AppDbContext context)
    : Repository<Student>(context), IStudentRepository
{
    public async Task<(List<Student> Items, int TotalCount)> GetPagedAsync(
        EnrollmentStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable();
        if (status.HasValue)
            query = query.Where(s => s.EnrollmentStatus == status.Value);
        else
            query = query.Where(s => s.EnrollmentStatus == EnrollmentStatus.Active);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<string> GetNextStudentCodeAsync(int enrollmentYear, CancellationToken ct = default)
    {
        var prefix = $"{enrollmentYear}-";
        var maxCode = await DbSet
            .Where(s => s.StudentCode.StartsWith(prefix))
            .MaxAsync(s => (string?)s.StudentCode, ct);

        var next = 1;
        if (maxCode is not null)
        {
            var parts = maxCode.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var seq))
                next = seq + 1;
        }

        return $"{enrollmentYear}-{next:D6}";
    }
}
```

The EF Core global query filter on `SchoolId` is automatically applied to all `DbSet` queries in this repository, so `GetNextStudentCodeAsync` only sees the current tenant's students — no manual `WHERE SchoolId = @id` clause needed.

#### `AppDbContext`

Add the new `DbSet` property:

```csharp
public DbSet<Student> Students => Set<Student>();
```

#### `DependencyInjection.cs` (Infrastructure)

Add to `AddInfrastructure`:

```csharp
services.AddScoped<IStudentRepository, StudentRepository>();
```

### WebApi (`SchoolMgmt.WebApi`)

#### `StudentsController`

All endpoints require `[Authorize(Roles = "Admin")]`. All state-mutating operations are POST/PUT — no GET side effects. No DELETE endpoint (students are never hard-deleted).

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `POST` | `/api/students` | `CreateStudentAsync` | 201 + `Location: /api/students/{id}` | Generates `StudentCode` automatically |
| `GET` | `/api/students` | `GetStudentsAsync` | 200 — `PagedResult<StudentSummaryDto>` | `status` query param (default: Active); `page` (default: 1); `pageSize` (default: 20, max: 100) |
| `GET` | `/api/students/{id}` | `GetStudentByIdAsync` | 200 — `StudentDto` | 404 if not found |
| `PUT` | `/api/students/{id}` | `UpdateStudentAsync` | 200 — `StudentDto` | 404 if not found; `StudentCode` is not updatable |

**`pageSize` cap:** the controller clamps `pageSize` to a maximum of 100 before passing to the service:

```csharp
pageSize = Math.Min(pageSize, 100);
```

No `DomainExceptionFilter` changes needed — `NotFoundException` (→ 404) and `DomainException` (→ 400) are already handled by the existing filter from spec #3/#4.

## Project Structure

New and modified files introduced by this spec:

```
backend/
  SchoolMgmt.Domain/
    Enums/
      Gender.cs                                   # new
      EnrollmentStatus.cs                         # new
    Entities/
      Student.cs                                  # new

  SchoolMgmt.Application/
    Interfaces/
      IUnitOfWork.cs                              # modified — add Detach<T>
    Students/
      IStudentRepository.cs                       # new
      StudentService.cs                           # new
      Dtos/
        StudentSummaryDto.cs                      # new
        StudentDto.cs                             # new
        PagedResult.cs                            # new (generic — reusable by future slices)
        CreateStudentRequest.cs                   # new
        UpdateStudentRequest.cs                   # new
      Validators/
        CreateStudentRequestValidator.cs          # new
        UpdateStudentRequestValidator.cs          # new
    DependencyInjection.cs                        # add StudentService registration

  SchoolMgmt.Infrastructure/
    Persistence/
      AppDbContext.cs                             # add DbSet<Student>
      UnitOfWork.cs                               # add Detach<T> implementation
      Configurations/
        StudentConfiguration.cs                   # new
      Repositories/
        StudentRepository.cs                      # new
      Migrations/
        <AddStudents>                             # new — EF Core generated
    DependencyInjection.cs                        # add IStudentRepository registration

  SchoolMgmt.WebApi/
    Controllers/
      StudentsController.cs                       # new

tests/
  SchoolMgmt.IntegrationTests/
    StudentsControllerTests.cs                    # new — real Postgres via Testcontainers
```

No domain unit tests — `Student` has no domain behavior (no guards, no invariants). All meaningful coverage is in the integration tests.

## Commands

```
Build:          dotnet build SchoolMgmt.slnx
Test:           dotnet test SchoolMgmt.slnx
Add migration:  dotnet ef migrations add AddStudents --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
Apply locally:  dotnet ef database update --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

## Database Schema Additions

### `Students`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | Surrogate primary key (globally unique) |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope |
| `StudentCode` | `varchar` | 20 | — | NOT NULL | — | UNIQUE with `SchoolId` | `YYYY-NNNNNN`; year = `EnrollmentDate.Year`; immutable after creation |
| `FirstName` | `varchar` | 100 | — | NOT NULL | — | — | — |
| `LastName` | `varchar` | 100 | — | NOT NULL | — | — | — |
| `DateOfBirth` | `date` | — | — | NOT NULL | — | — | `DateOnly` → PostgreSQL `date` |
| `Gender` | `varchar` | 20 | — | NOT NULL | — | — | Stored as string: `"Male"` / `"Female"` / `"Other"` |
| `EnrollmentDate` | `date` | — | — | NOT NULL | — | — | `DateOnly` → PostgreSQL `date` |
| `EnrollmentStatus` | `varchar` | 20 | — | NOT NULL | — | — | Stored as string: `"Active"` / `"Transferred"` / `"Graduated"` / `"Dropped"` |
| `GuardianName` | `varchar` | 200 | — | NULL | — | — | — |
| `GuardianPhone` | `varchar` | 20 | — | NULL | — | — | — |
| `GuardianEmail` | `varchar` | 256 | — | NULL | — | — | — |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | Auto-stamped by `AppDbContext.SaveChangesAsync` |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | Auto-stamped on modification |

## Testing Strategy

No domain unit tests — `Student` has no domain logic to isolate.

### Integration tests (`SchoolMgmt.IntegrationTests`)

Against real Postgres (Testcontainers), authenticated as the demo Admin:

**Create:**
- `POST /api/students` with valid data → 201; response body contains `id`, `studentCode` in `YYYY-NNNNNN` format, `enrollmentStatus = "Active"`, guardian fields
- `POST /api/students` with missing `firstName` → 400 (FluentValidation)
- `POST /api/students` with invalid `gender` value → 400
- `POST /api/students` with invalid `guardianEmail` → 400
- Two sequential creates in the same enrollment year → `studentCode` sequence increments (e.g. `2025-000001` then `2025-000002`)
- Create with `enrollmentDate` year 2024 → `studentCode` starts with `2024-`

**Read:**
- `GET /api/students` (no params) → returns only Active students
- `GET /api/students?status=Transferred` → returns only Transferred students
- `GET /api/students?page=1&pageSize=5` → returns up to 5 items; `totalCount` and `page` fields present in response
- `GET /api/students?pageSize=200` → clamped to 100 items max
- `GET /api/students/{id}` → 200 with full `StudentDto` including guardian fields
- `GET /api/students/{unknownId}` → 404

**Update:**
- `PUT /api/students/{id}` with updated name → 200; response reflects new name; `updatedAt` is set
- `PUT /api/students/{id}` with `enrollmentStatus = "Transferred"` → 200; student no longer appears in default `GET /api/students` (Active filter)
- `PUT /api/students/{id}` with invalid `enrollmentStatus` → 400
- `PUT /api/students/{unknownId}` → 404
- `PUT /api/students/{id}` — verify `studentCode` in the response is unchanged from the original create

**No delete:**
- `DELETE /api/students/{id}` → 405 (Method Not Allowed; no route registered)

**Auth:**
- Unauthenticated request to any endpoint → 401
- Request with Teacher role → 403

## Boundaries

- **Always:** call `dotnet test SchoolMgmt.slnx` before considering any task done; follow the DI/layering rules in `.claude/rules/backend.md`; keep GET endpoints side-effect-free; never update `StudentCode` in `UpdateStudentAsync` (it is permanently immutable).
- **Ask first:** adding a `SectionId` FK to `Student` (class/section assignment is explicitly a separate spec); exposing a PATCH endpoint for status-only transitions (not in scope for this spec); changing the `PagedResult<T>` response shape (may affect frontend contract).
- **Never:** hard-delete a student (no DELETE endpoint; no `Remove` call on `Student` entities from any service); call `SaveChangesAsync` or transaction methods from inside `StudentRepository`; add `IStudentRepository` methods that do cross-tenant queries (all queries must go through the EF global query filter on `SchoolId`).

## Success Criteria

- `POST /api/students` returns 201 with a `studentCode` matching `YYYY-NNNNNN` where `YYYY` equals the request's `enrollmentDate` year — integration test passes.
- Two sequential creates in the same year produce codes `YYYY-000001` and `YYYY-000002` — integration test confirms sequential generation.
- `GET /api/students` (no status param) returns only `Active` students — integration test passes after a student is set to `Transferred`.
- `PUT /api/students/{id}` cannot change `studentCode` — integration test verifies the code is unchanged in the update response.
- `DELETE /api/students/{id}` returns 405 — no delete route registered.
- All `GET /api/students/{id}` responses for unknown IDs return 404.
- All endpoints return 401 for unauthenticated requests and 403 for Teacher-role requests.
- All tests in `dotnet test SchoolMgmt.slnx` pass.

## Open Questions

None — all design decisions resolved in the idea doc and the ideation session.
