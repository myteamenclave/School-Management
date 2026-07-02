# Spec: Implement Teacher CRUD

## Related docs & specs

- [docs/ideas/04-teacher-crud.md](../docs/ideas/04-teacher-crud.md) — idea doc: problem statement, two-table rationale, TeacherCode format, deactivation/login-disable decision, not-doing list
- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — Teacher role definition; teachers mark attendance and enter grades (future specs build on the accounts created here)
- [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md) — `BaseEntity`, `ITenantScoped`, `IRepository<TEntity>`, `IUnitOfWork`, `AppDbContext`; all used as-is
- [specs/02-implement-auth.md](02-implement-auth.md) — `User` entity, `UserRole`, `AuthService`; this spec modifies `User` (adds `IsActive`) and patches `AuthService.LoginAsync` to respect it
- [specs/05-implement-student-crud.md](05-implement-student-crud.md) — primary structural reference: `YYYY-NNNNNN` code generation, retry-on-collision pattern, `IOptions`-based config, paged list, `IUnitOfWork.Detach<T>`, `PagedResult<T>` (already implemented, reused here)
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller/Application-service pattern, GET-must-be-read-only, Repository/UnitOfWork rules, DI conventions

## Objective

Implement Admin-only CRUD for `Teacher` — user accounts that teachers need to log in and later mark attendance and enter grades. A teacher record is two things at once: a `User` (auth layer, already exists) and a domain profile (`Teacher` entity, new). Admin creates both atomically in one transaction. The `TeacherCode` (`YYYY-NNNNNN`, joining year) mirrors the `StudentCode` pattern.

This spec also adds `User.IsActive` so that deactivating a teacher disables their login — the `AuthService.LoginAsync` check is a one-line addition but it requires a migration that touches the existing `Users` table.

**Out of scope for this spec:** email or password updates via this API (auth concern, deferred), forced password-change on first login, class/subject assignment (separate spec), bulk CSV import (separate spec), parent accounts (separate spec).

## Tech Stack

- .NET 8.0, C# — same solution as all prior specs
- EF Core (Npgsql) — one new table (`Teachers`), one new column on `Users` (`IsActive`)
- FluentValidation — request validators via the existing global `IAsyncActionFilter`
- xUnit + `WebApplicationFactory` + Testcontainers (Postgres) — same integration test setup

## Design

### Domain — modified: `SchoolMgmt.Domain`

#### Modified: `User`

Add one property to the existing `User` entity:

```csharp
public bool IsActive { get; set; } = true;
```

No other changes to `User`. `DisplayName` (the existing field) is set to `$"{firstName} {lastName}"` at create time — the Teacher entity holds `FirstName`/`LastName` as the canonical profile fields; `User.DisplayName` is derived and stored for JWT claims only.

#### New: `Teacher`

```csharp
// SchoolMgmt.Domain/Entities/Teacher.cs
namespace SchoolMgmt.Domain.Entities;

public class Teacher : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;        // navigation property — loaded via Include where needed

    public string TeacherCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly JoiningDate { get; set; }
    public bool IsActive { get; set; } = true;
}
```

**Design notes:**
- `UserId` FK + `User` navigation property: the service loads the full graph (`Include(t => t.User)`) only when it needs to write back to `User.IsActive` (deactivation path). List and detail reads use teacher fields only — no join needed.
- `TeacherCode` is immutable once assigned (same rule as `StudentCode`).
- `SchoolId` is auto-stamped by `AppDbContext.SaveChangesAsync` on first save, same as all `ITenantScoped` entities. `User.SchoolId` is set explicitly from `ITenantProvider.CurrentSchoolId` before saving (teacher creation runs as an authenticated Admin, so the tenant IS resolvable — unlike the pre-auth login/refresh paths in spec #2).
- `Teacher.IsActive` and `User.IsActive` are kept in sync by `TeacherService.UpdateTeacherAsync` whenever `isActive` changes. They are never allowed to diverge.

### Application layer — `SchoolMgmt.Application`

#### Auth patch: `AuthService.LoginAsync`

Add one check after the password verification:

```csharp
public async Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
{
    var user = await users.GetByEmailAsync(request.Email, cancellationToken);
    if (user is null || !passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        return null;

    if (!user.IsActive)    // ← new: inactive users cannot log in
        return null;

    return await IssueTokensAsync(user, sessionId: Guid.NewGuid(), tokenToReplace: null, cancellationToken);
}
```

Returning `null` (same path as wrong password) is deliberate — no signal to the caller about *why* login failed.

#### `ITeacherRepository`

```csharp
// SchoolMgmt.Application/Teachers/ITeacherRepository.cs
namespace SchoolMgmt.Application.Teachers;

public interface ITeacherRepository : IRepository<Teacher>
{
    Task<(List<Teacher> Items, int TotalCount)> GetPagedAsync(
        bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<Teacher?> GetByIdWithUserAsync(Guid id, CancellationToken ct = default);

    Task<string> GetNextTeacherCodeAsync(int joiningYear, CancellationToken ct = default);
}
```

- `GetPagedAsync`: when `isActive` is null, defaults to active-only (same default-to-active pattern as `StudentRepository.GetPagedAsync`).
- `GetByIdWithUserAsync`: loads `Teacher` + `User` via `Include(t => t.User)` — used by update (needs User for IsActive sync) and detail read (needs email for the full `TeacherDto`).
- `GetByIdAsync(Guid id)` (from `IRepository<Teacher>`) is also available but not used in this spec — `GetByIdWithUserAsync` covers all reads.

#### DTOs

```csharp
// SchoolMgmt.Application/Teachers/Dtos/

public record TeacherSummaryDto(
    Guid Id,            // Teacher.Id
    string TeacherCode,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly JoiningDate,
    bool IsActive,
    string Email        // from Teacher.User.Email — included in list for Admin convenience
);

public record TeacherDto(
    Guid Id,
    string TeacherCode,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly JoiningDate,
    bool IsActive,
    string Email,
    Guid UserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

// PagedResult<T> already exists in Application/Students/Dtos/ — reuse it

public record CreateTeacherRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly JoiningDate
);

public record UpdateTeacherRequest(
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly JoiningDate,
    bool IsActive
);
```

Email and Password are NOT in `UpdateTeacherRequest` — they are auth concerns and deliberately excluded.

#### Validators (FluentValidation)

```csharp
// CreateTeacherRequestValidator
RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
RuleFor(x => x.Phone).MaximumLength(20).When(x => x.Phone is not null);
RuleFor(x => x.JoiningDate).NotEmpty();

// UpdateTeacherRequestValidator
RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
RuleFor(x => x.Phone).MaximumLength(20).When(x => x.Phone is not null);
RuleFor(x => x.JoiningDate).NotEmpty();
```

#### `TeacherService`

One service, injecting `ITeacherRepository`, `IUserRepository`, `IPasswordHasher`, `IUnitOfWork`, and `IOptions<TeacherOptions>`. `IPasswordHasher` is already defined in Application (spec #2 pattern); `IUserRepository` is already defined.

**`TeacherOptions`** — a new options class registered via `IOptions<T>` pattern:

```csharp
// SchoolMgmt.Application/Teachers/TeacherOptions.cs
public class TeacherOptions
{
    public int TeacherCodeMaxRetries { get; set; } = 3;
}
```

**Service method signatures:**

```
CreateTeacherAsync(CreateTeacherRequest, CancellationToken) → TeacherDto
GetTeachersAsync(bool? isActive, int page, int pageSize, CancellationToken) → PagedResult<TeacherSummaryDto>
GetTeacherByIdAsync(Guid id, CancellationToken) → TeacherDto
UpdateTeacherAsync(Guid id, UpdateTeacherRequest, CancellationToken) → TeacherDto
```

**`CreateTeacherAsync` algorithm:**

```
CreateTeacherAsync(CreateTeacherRequest, CancellationToken) → TeacherDto
  await unitOfWork.BeginTransactionAsync(ct)
  try:
    user = new User
      { Email = request.Email,
        PasswordHash = passwordHasher.HashPassword(request.Password),
        DisplayName = $"{request.FirstName} {request.LastName}",
        Role = UserRole.Teacher,
        IsActive = true }
    await userRepository.AddAsync(user, ct)
    await unitOfWork.SaveChangesAsync(ct)   // ← flush User so user.Id is available for the FK

    teacher = new Teacher
      { UserId = user.Id, TeacherCode = string.Empty,
        FirstName = request.FirstName, LastName = request.LastName,
        Phone = request.Phone, JoiningDate = request.JoiningDate, IsActive = true }

    for attempt in 0.._maxRetries:
      teacher.TeacherCode = await teacherRepository.GetNextTeacherCodeAsync(request.JoiningDate.Year, ct)
      await teacherRepository.AddAsync(teacher, ct)
      try:
        await unitOfWork.SaveChangesAsync(ct)
        break  // success — teacher inserted
      catch ConflictException when attempt < _maxRetries - 1:
        unitOfWork.Detach(teacher)
        // loop: generate a new code and retry
    else:
      throw DomainException("Unable to assign a teacher code. Please try again.")

    await unitOfWork.CommitAsync(ct)
    teacher.User = user        // attach navigation property for ToDto mapping
    return ToDto(teacher)
  catch:
    await unitOfWork.RollbackAsync(ct)
    throw
```

**Why two `SaveChangesAsync` calls within the transaction:** The first flush is needed so EF Core generates `user.Id` (the `uuid` PK), which is required for `teacher.UserId`. Both flushes live inside the outer transaction — only `CommitAsync` makes them durable.

**Why not populate `teacher.User` from EF after save:** After the second `SaveChangesAsync`, EF tracks the entity but the `User` navigation property is null until we set it manually (the `Include` happens at load time, not at insert time). Setting `teacher.User = user` after commit is safe — `user` is already fully populated with its `Id` at this point.

```
GetTeachersAsync(bool? isActive, int page, int pageSize, CancellationToken) → PagedResult<TeacherSummaryDto>
  (items, total) = await repository.GetPagedAsync(isActive, page, pageSize, ct)
  return new PagedResult<TeacherSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize)

GetTeacherByIdAsync(Guid id, CancellationToken) → TeacherDto
  teacher = await repository.GetByIdWithUserAsync(id, ct)
      ?? throw new NotFoundException("Teacher not found.")
  return ToDto(teacher)

UpdateTeacherAsync(Guid id, UpdateTeacherRequest, CancellationToken) → TeacherDto
  teacher = await repository.GetByIdWithUserAsync(id, ct)
      ?? throw new NotFoundException("Teacher not found.")
  teacher.FirstName = request.FirstName
  teacher.LastName = request.LastName
  teacher.Phone = request.Phone
  teacher.JoiningDate = request.JoiningDate
  if teacher.IsActive != request.IsActive:
    teacher.IsActive = request.IsActive
    teacher.User.IsActive = request.IsActive    // keep in sync atomically
    userRepository.Update(teacher.User)
  repository.Update(teacher)
  await unitOfWork.SaveChangesAsync(ct)
  return ToDto(teacher)
  Note: TeacherCode and Email are NOT updated — immutable / auth concern.
```

The `if teacher.IsActive != request.IsActive` guard avoids a redundant write to `Users` on every update.

#### `DependencyInjection.cs` (Application)

Append to `AddApplication`:

```csharp
services.AddScoped<TeacherService>();
services.Configure<TeacherOptions>(configuration.GetSection("Teachers"));
```

`TeacherOptions` needs `IConfiguration` — add `IConfiguration configuration` parameter to `AddApplication` (same pattern that `AddInfrastructure` already uses).

### Infrastructure — `SchoolMgmt.Infrastructure`

#### Modified: `UserConfiguration`

Add one property mapping:

```csharp
builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
```

No seed data changes — `DemoDataSeeder` creates the demo Admin user in code; it will construct `User` with `IsActive = true` by default.

#### `TeacherConfiguration`

```csharp
// Table: Teachers
builder.ToTable("Teachers");
builder.HasKey(x => x.Id);

builder.HasOne(t => t.User)
    .WithOne()
    .HasForeignKey<Teacher>(t => t.UserId)
    .OnDelete(DeleteBehavior.Restrict);  // never cascade-delete a User via Teacher delete

builder.Property(x => x.TeacherCode).IsRequired().HasMaxLength(20);
builder.HasIndex(x => new { x.SchoolId, x.TeacherCode }).IsUnique();

builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
builder.Property(x => x.Phone).HasMaxLength(20);
builder.Property(x => x.JoiningDate).IsRequired().HasColumnType("date");
builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
```

`OnDelete(DeleteBehavior.Restrict)` — there is no delete endpoint in this spec. This guard is defensive: if a hard-delete was accidentally coded against either table, the FK constraint would prevent orphaned records.

#### `TeacherRepository`

```csharp
internal sealed class TeacherRepository(AppDbContext context)
    : Repository<Teacher>(context), ITeacherRepository
{
    public async Task<(List<Teacher> Items, int TotalCount)> GetPagedAsync(
        bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.Include(t => t.User).AsQueryable();
        query = isActive.HasValue
            ? query.Where(t => t.IsActive == isActive.Value)
            : query.Where(t => t.IsActive);   // default: active only

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Teacher?> GetByIdWithUserAsync(Guid id, CancellationToken ct = default)
        => await DbSet.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<string> GetNextTeacherCodeAsync(int joiningYear, CancellationToken ct = default)
    {
        var prefix = $"{joiningYear}-";
        var maxCode = await DbSet
            .Where(t => t.TeacherCode.StartsWith(prefix))
            .MaxAsync(t => (string?)t.TeacherCode, ct);

        var next = 1;
        if (maxCode is not null)
        {
            var parts = maxCode.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var seq))
                next = seq + 1;
        }

        return $"{joiningYear}-{next:D6}";
    }
}
```

The EF Core global query filter on `SchoolId` is applied automatically to all `DbSet` queries — `GetNextTeacherCodeAsync` only sees the current tenant's teachers, exactly like `GetNextStudentCodeAsync`.

#### Modified: `AppDbContext`

Add:

```csharp
public DbSet<Teacher> Teachers => Set<Teacher>();
```

#### `DependencyInjection.cs` (Infrastructure)

Add to `AddInfrastructure`:

```csharp
services.AddScoped<ITeacherRepository, TeacherRepository>();
```

### WebApi — `SchoolMgmt.WebApi`

#### `TeachersController`

All endpoints `[Authorize(Roles = "Admin")]`. All state-mutating operations are POST/PUT — no GET side effects. No DELETE endpoint (no hard delete).

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `POST` | `/api/teachers` | `CreateTeacherAsync` | 201 + `Location: /api/teachers/{id}` | Creates User + Teacher atomically; `{id}` = Teacher.Id |
| `GET` | `/api/teachers` | `GetTeachersAsync` | 200 — `PagedResult<TeacherSummaryDto>` | `isActive` query param (default: active only, no param needed); `page` (default: 1); `pageSize` (default: 20, max: 100) |
| `GET` | `/api/teachers/{id}` | `GetTeacherByIdAsync` | 200 — `TeacherDto` | 404 if not found |
| `PUT` | `/api/teachers/{id}` | `UpdateTeacherAsync` | 200 — `TeacherDto` | 404 if not found; `teacherCode` and `email` are NOT updatable |

`pageSize` cap: clamp to 100 in the controller, same as `StudentsController`:

```csharp
pageSize = Math.Min(pageSize, 100);
```

## Project Structure

New and modified files introduced by this spec:

```
backend/
  SchoolMgmt.Domain/
    Entities/
      User.cs                                      # modified — add IsActive property
      Teacher.cs                                   # new

  SchoolMgmt.Application/
    Auth/
      AuthService.cs                               # modified — add IsActive check in LoginAsync
    Teachers/
      ITeacherRepository.cs                        # new
      TeacherService.cs                            # new
      TeacherOptions.cs                            # new
      Dtos/
        TeacherSummaryDto.cs                       # new
        TeacherDto.cs                              # new
        CreateTeacherRequest.cs                    # new
        UpdateTeacherRequest.cs                    # new
      Validators/
        CreateTeacherRequestValidator.cs           # new
        UpdateTeacherRequestValidator.cs           # new
    DependencyInjection.cs                         # add TeacherService + TeacherOptions config

  SchoolMgmt.Infrastructure/
    Persistence/
      AppDbContext.cs                              # add DbSet<Teacher>
      Configurations/
        UserConfiguration.cs                      # modified — add IsActive mapping
        TeacherConfiguration.cs                   # new
      Repositories/
        TeacherRepository.cs                      # new
      Migrations/
        <AddTeachersWithUserIsActive>              # new — EF Core generated (covers both changes)
    DependencyInjection.cs                        # add ITeacherRepository registration

  SchoolMgmt.WebApi/
    Controllers/
      TeachersController.cs                       # new

tests/
  SchoolMgmt.IntegrationTests/
    TeachersControllerTests.cs                    # new — real Postgres via Testcontainers
```

One migration covers both the new `Teachers` table and the `IsActive` column on `Users`. Name it `AddTeachersWithUserIsActive`.

## Commands

```
Build:          dotnet build SchoolMgmt.slnx
Test:           dotnet test SchoolMgmt.slnx
Add migration:  dotnet ef migrations add AddTeachersWithUserIsActive --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
Apply locally:  dotnet ef database update --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

## Database Schema Additions / Modifications

### Modified: `Users`

| Column | PG Type | Default | Nullable | Notes |
|---|---|---|---|---|
| `IsActive` | `boolean` | `true` | NOT NULL | New column; default `true` ensures all existing rows (the seeded demo Admin) remain active after the migration |

### New: `Teachers`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | Surrogate primary key |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope |
| `UserId` | `uuid` | — | — | NOT NULL | — | FK → Users(Id), Restrict | One-to-one link to the auth user |
| `TeacherCode` | `varchar` | 20 | — | NOT NULL | — | UNIQUE with `SchoolId` | `YYYY-NNNNNN`; year = `JoiningDate.Year`; immutable |
| `FirstName` | `varchar` | 100 | — | NOT NULL | — | — | — |
| `LastName` | `varchar` | 100 | — | NOT NULL | — | — | — |
| `Phone` | `varchar` | 20 | — | NULL | — | — | — |
| `JoiningDate` | `date` | — | — | NOT NULL | — | — | `DateOnly` → PostgreSQL `date` |
| `IsActive` | `boolean` | — | `true` | NOT NULL | — | — | Synced with `User.IsActive` on every update |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | Auto-stamped by `AppDbContext` |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | Auto-stamped on modification |

## Testing Strategy

No domain unit tests — `Teacher` has no domain behavior (no guards, no invariants).

### Integration tests (`SchoolMgmt.IntegrationTests`)

Against real Postgres (Testcontainers), authenticated as the demo Admin:

**Create:**
- `POST /api/teachers` with valid data → 201; response body contains `id`, `teacherCode` in `YYYY-NNNNNN` format, `isActive = true`, email
- `POST /api/teachers` with missing `firstName` → 400
- `POST /api/teachers` with invalid email → 400
- `POST /api/teachers` with password shorter than 8 chars → 400
- Two sequential creates in the same joining year → `teacherCode` sequence increments (e.g. `2026-000001` then `2026-000002`)
- Create with `joiningDate` in 2025 → `teacherCode` starts with `2025-`
- `POST /api/teachers` with a duplicate email → 409 (ConflictException from unique index on `(SchoolId, Email)` in `Users`)

**Read:**
- `GET /api/teachers` (no params) → returns only active teachers
- `GET /api/teachers?isActive=false` → returns only inactive teachers
- `GET /api/teachers?page=1&pageSize=5` → returns up to 5 items; `totalCount` and `page` fields present
- `GET /api/teachers?pageSize=200` → clamped to 100
- `GET /api/teachers/{id}` → 200 with full `TeacherDto` including `email`, `userId`
- `GET /api/teachers/{unknownId}` → 404

**Update:**
- `PUT /api/teachers/{id}` with updated name → 200; response reflects new name; `updatedAt` is set
- `PUT /api/teachers/{id}` with `isActive = false` → 200; teacher no longer appears in default `GET /api/teachers`
- `PUT /api/teachers/{id}` with `isActive = false`, then attempt login as that teacher → 401 (disabled login)
- `PUT /api/teachers/{id}` — verify `teacherCode` in response is unchanged from create
- `PUT /api/teachers/{id}` — verify `email` in response is unchanged from create
- `PUT /api/teachers/{unknownId}` → 404

**No delete:**
- `DELETE /api/teachers/{id}` → 405

**Auth:**
- Unauthenticated request to any endpoint → 401
- Request with Teacher role → 403

**Auth regression (from `User.IsActive` change):**
- Demo Admin (seeded with `IsActive` defaulting to `true`) can still log in after the migration — integration test confirms login still works after the `AddTeachersWithUserIsActive` migration runs

## Boundaries

- **Always:** call `dotnet test SchoolMgmt.slnx` before considering any task done; follow the DI/layering rules in `.claude/rules/backend.md`; keep GET endpoints side-effect-free; never update `TeacherCode` in `UpdateTeacherAsync`; always sync `Teacher.IsActive` and `User.IsActive` in the same `SaveChangesAsync` call.
- **Ask first:** exposing email/password update via this API (explicitly deferred to an auth-management spec); adding `EmploymentStatus` enum instead of `IsActive` bool (requires a schema change and a new migration); changing `Teacher.Id` to be `UserId` directly (the spec uses a separate PK — don't collapse without discussion).
- **Never:** hard-delete a teacher; hard-delete a user created for a teacher; call `SaveChangesAsync` or transaction methods from inside `TeacherRepository`; let `Teacher.IsActive` and `User.IsActive` diverge; put email/password in `UpdateTeacherRequest`.

## Success Criteria

- `POST /api/teachers` returns 201 with `teacherCode` matching `YYYY-NNNNNN` where `YYYY` = `joiningDate` year — integration test passes.
- Two sequential creates in the same year produce codes `YYYY-000001` and `YYYY-000002` — integration test confirms sequential generation.
- A teacher created via the API can log in via `POST /api/auth/login` with the password set at creation time — integration test confirms.
- `PUT /api/teachers/{id}` with `isActive = false` → subsequent login attempt for that teacher's email returns 401 — integration test confirms.
- `GET /api/teachers` (no params) returns only active teachers — integration test verifies after one teacher is deactivated.
- `DELETE /api/teachers/{id}` returns 405 — no route registered.
- All `GET /api/teachers/{id}` for unknown IDs return 404.
- All endpoints return 401 for unauthenticated requests and 403 for Teacher-role requests.
- Demo Admin login still works after the migration (regression guard).
- All tests in `dotnet test SchoolMgmt.slnx` pass.

## Open Questions

None — all design decisions resolved in the idea doc and the ideation session.
