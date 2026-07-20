# Spec: Implement Parent–Guardian Link

## Related docs & specs

- [docs/ideas/14-parent-guardian-link.md](../docs/ideas/14-parent-guardian-link.md) — idea doc: problem statement, "convert `GuardianEmail` into a Parent login" direction, `StudentParent` junction rationale, account-creation flow, resolved open questions, not-doing list
- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — source idea: the parent fee-payment flow that motivated a `Parent` role in the first place
- [specs/02-implement-auth.md](02-implement-auth.md) — provides the `User` entity, `UserRole.Parent`, `IPasswordHasher`, `IUserRepository`, the `(SchoolId, Email)` unique index, and JWT-cookie login. This spec creates the **first runtime user-creation path** (all prior `User` rows come from `DemoDataSeeder`); a Parent created here logs in through the existing `POST /api/auth/login` unchanged
- [specs/05-implement-student-crud.md](05-implement-student-crud.md) — provides `Student` with the inline nullable `GuardianEmail`/`GuardianName` this feature converts into a login; Admin-only controller pattern reused here
- [specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md) — the `StudentSectionEnrollment` / `TeacherSectionSubject` junction entities this spec's `StudentParent` mirrors exactly (BaseEntity + ITenantScoped, composite unique index, `OnDelete.Restrict` FKs)
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller/Application-service pattern, DI conventions, GET-must-be-read-only, Repository/UnitOfWork rules

## Objective

Let an Admin turn a student's existing `GuardianEmail` into a **Parent login** and link that Parent user to the student, without a separate registration flow. A `StudentParent` junction table connects Parent `User`s to `Student`s as many-to-many (one parent → many children; one student → many parents). All operations are **Admin-only**; there is no parent-facing data access, no self-registration, and no self-service password reset in this slice.

Success looks like: from a student that has a `GuardianEmail`, an Admin calls `POST /api/students/{id}/parent-login` with a temporary password; the system creates a `Parent`-role `User` (or reuses an existing Parent account with that email) and creates the `StudentParent` link; the Admin can then list linked parents and remove a link. The created Parent can immediately log in via the existing auth endpoints.

**Out of scope for this spec (deferred / not doing):**
- All frontend work — the Student Detail "Parent Accounts" UI is a separate `-B` spec (matches the student-CRUD split).
- Any parent-facing read endpoints (a logged-in parent viewing their child's attendance/grades/fees) — a future spec.
- Self-registration, email/SMTP invites, self-service password reset, force-password-change-on-first-login, and parent-account deactivation — all explicitly deferred per the idea doc.
- Resetting/overwriting the password of an **already-existing** Parent account (reuse path leaves the existing password untouched — see design).

## Tech Stack

- .NET 8.0, C# — same solution structure and Clean Architecture layering as specs #1–#16
- EF Core (Npgsql) — one new table (`StudentParents`), one new migration
- FluentValidation — request validator registered via the existing global `IAsyncActionFilter`
- `IPasswordHasher` (from spec #2) — hashes the Admin-set temporary password; no new crypto
- xUnit + hand-written fakes (unit) and `WebApplicationFactory` + Testcontainers/Postgres (integration) — same setup as prior specs

## Design

### Domain (`SchoolMgmt.Domain`)

#### `StudentParent` junction entity

Mirrors `StudentSectionEnrollment` exactly (BaseEntity + ITenantScoped, explicit `SchoolId`, navigation properties). No factory method — no multi-field invariant; object initializer is correct.

```csharp
// SchoolMgmt.Domain/Entities/StudentParent.cs
using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class StudentParent : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid UserId { get; set; }        // the Parent-role User
    public User ParentUser { get; set; } = null!;
}
```

No new enum — `UserRole.Parent` already exists (spec #2). `SchoolId` is auto-stamped by `AppDbContext.SaveChangesAsync` from `ITenantProvider.CurrentSchoolId`, same as every other `ITenantScoped` entity. `BaseEntity.Id` is assigned `Guid.NewGuid()` at construction, so a freshly-`new`ed Parent `User`'s `Id` is available to set on the link before the single `SaveChangesAsync` — no intermediate save needed.

### Application layer (`SchoolMgmt.Application`)

New feature folder `ParentAccounts/` (namespace `SchoolMgmt.Application.ParentAccounts`), organized exactly like `Students/`:

```
ParentAccounts/
  IStudentParentRepository.cs
  ParentAccountService.cs
  Dtos/
    CreateParentLoginRequest.cs
    ParentLoginResultDto.cs
    ParentAccountDto.cs
  Validators/
    CreateParentLoginRequestValidator.cs
```

#### `IUserRepository` — one addition

The existing `GetByEmailAsync` **bypasses** the tenant query filter (a deliberate pre-auth exception for login). This feature runs inside an authenticated Admin request and must **not** look across tenants, so add a tenant-scoped lookup (respects the global query filter):

```csharp
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);         // existing — IgnoreQueryFilters (pre-auth only)
    Task<User?> FindByEmailInTenantAsync(string email, CancellationToken cancellationToken = default); // new — respects the tenant filter
}
```

`FindByEmailInTenantAsync` in `UserRepository` is a plain `DbSet.FirstOrDefaultAsync(u => u.Email == email, ct)` — no `IgnoreQueryFilters()`, so the global `SchoolId` filter applies automatically. This keeps the collision/reuse check scoped to the current school, matching the `(SchoolId, Email)` unique index.

#### `IStudentParentRepository`

```csharp
// SchoolMgmt.Application/ParentAccounts/IStudentParentRepository.cs
namespace SchoolMgmt.Application.ParentAccounts;

public interface IStudentParentRepository : IRepository<StudentParent>
{
    Task<StudentParent?> GetLinkAsync(Guid studentId, Guid userId, CancellationToken ct = default);
    Task<List<StudentParent>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default); // Include ParentUser, ordered by ParentUser.DisplayName
}
```

#### DTOs

```csharp
public record CreateParentLoginRequest(string TemporaryPassword);

// Result of the create/link call. Frontend uses the flags to message the Admin correctly.
public record ParentLoginResultDto(
    Guid ParentUserId,
    string Email,
    string DisplayName,
    bool AccountCreated,   // true = new Parent user created with this temp password;
                           // false = an existing Parent account was reused (temp password IGNORED)
    bool LinkCreated       // true = a new StudentParent link was created;
                           // false = the link already existed (idempotent no-op)
);

public record ParentAccountDto(
    Guid ParentUserId,
    string Email,
    string DisplayName,
    DateTimeOffset AccountCreatedAt   // the Parent User's CreatedAt
);
```

The temporary password is **not** echoed in `ParentLoginResultDto` — the Admin already typed it and the frontend holds it; echoing a secret back adds no value. The `AccountCreated` flag tells the frontend whether that typed password is actually the parent's password (new account) or must be disregarded (reused account keeps its old password).

#### Validator

```csharp
// CreateParentLoginRequestValidator
RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
```

`TemporaryPassword` is always required by the validator even though it is ignored on the reuse path — the caller cannot know in advance whether the email maps to an existing Parent, so the field is unconditionally supplied.

#### `ParentAccountService`

One service, direct DI, private `ToDto` helper; no AutoMapper.

```csharp
public class ParentAccountService(
    IStudentRepository students,
    IUserRepository users,
    IStudentParentRepository links,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
```

**`CreateParentLoginAsync(Guid studentId, CreateParentLoginRequest request, CancellationToken ct) → ParentLoginResultDto`**

```
1. student = await students.GetByIdAsync(studentId, ct)
       ?? throw new NotFoundException("Student not found.")
2. email = student.GuardianEmail?.Trim()
   if string.IsNullOrEmpty(email):
       throw new DomainException("Student has no guardian email. Add one before creating a parent login.")
3. existing = await users.FindByEmailInTenantAsync(email, ct)
   accountCreated = false
   if existing is not null:
       if existing.Role != UserRole.Parent:
           throw new ConflictException($"The email '{email}' already belongs to a non-parent account and cannot be used for a parent login.")
       parent = existing                       // reuse — do NOT touch its PasswordHash
   else:
       parent = new User {
           Email = email,
           PasswordHash = passwordHasher.HashPassword(request.TemporaryPassword),
           DisplayName = string.IsNullOrWhiteSpace(student.GuardianName) ? email : student.GuardianName!,
           Role = UserRole.Parent,
           IsActive = true
       }
       await users.AddAsync(parent, ct)
       accountCreated = true
4. linkCreated = false
   existingLink = await links.GetLinkAsync(studentId, parent.Id, ct)
   if existingLink is null:
       await links.AddAsync(new StudentParent { StudentId = studentId, UserId = parent.Id }, ct)
       linkCreated = true
5. if accountCreated || linkCreated:
       await unitOfWork.SaveChangesAsync(ct)     // single atomic unit of work
6. return new ParentLoginResultDto(parent.Id, parent.Email, parent.DisplayName, accountCreated, linkCreated)
```

Notes:
- `GetLinkAsync` finding a `parent.Id` for a not-yet-persisted new user is fine — a brand-new user has no links, so it returns null and the link is created; both the user and link persist together in step 5.
- The whole call is idempotent: calling it twice for the same student+email returns `AccountCreated=false, LinkCreated=false` the second time and mutates nothing.
- No explicit transaction needed — a single `SaveChangesAsync` persists the new `User` and the new `StudentParent` atomically.

**`GetParentsForStudentAsync(Guid studentId, CancellationToken ct) → List<ParentAccountDto>`**

```
1. _ = await students.GetByIdAsync(studentId, ct) ?? throw new NotFoundException("Student not found.")
2. list = await links.GetByStudentIdAsync(studentId, ct)
3. return list.Select(l => new ParentAccountDto(l.ParentUser.Id, l.ParentUser.Email, l.ParentUser.DisplayName, l.ParentUser.CreatedAt)).ToList()
```

**`RemoveParentLinkAsync(Guid studentId, Guid parentUserId, CancellationToken ct) → void`**

```
1. link = await links.GetLinkAsync(studentId, parentUserId, ct)
       ?? throw new NotFoundException("Parent link not found for this student.")
2. links.Remove(link)                    // removes the LINK only — never the User
3. await unitOfWork.SaveChangesAsync(ct)
```

The Parent `User` is intentionally never deleted here — they may still be linked to other children. Account deactivation is out of scope.

#### `DependencyInjection.cs` (Application)

Append to `AddApplication`:

```csharp
services.AddScoped<ParentAccountService>();
```

Validator is picked up by the existing `AddValidatorsFromAssembly(...)` call.

### Infrastructure (`SchoolMgmt.Infrastructure`)

#### `StudentParentConfiguration`

```csharp
// Table: StudentParents
builder.ToTable("StudentParents");
builder.HasKey(x => x.Id);
builder.HasIndex(x => new { x.StudentId, x.UserId }).IsUnique();   // one link per (student, parent)

builder.HasOne(x => x.Student)
    .WithMany()
    .HasForeignKey(x => x.StudentId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.ParentUser)
    .WithMany()
    .HasForeignKey(x => x.UserId)
    .OnDelete(DeleteBehavior.Restrict);
```

`OnDelete.Restrict` on both FKs matches the codebase convention (no cascade deletes through junctions).

#### `UserRepository` — `FindByEmailInTenantAsync`

```csharp
public Task<User?> FindByEmailInTenantAsync(string email, CancellationToken ct = default) =>
    DbSet.FirstOrDefaultAsync(u => u.Email == email, ct);   // NO IgnoreQueryFilters — tenant-scoped
```

#### `StudentParentRepository`

```csharp
internal sealed class StudentParentRepository(AppDbContext context)
    : Repository<StudentParent>(context), IStudentParentRepository
{
    public Task<StudentParent?> GetLinkAsync(Guid studentId, Guid userId, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(x => x.StudentId == studentId && x.UserId == userId, ct);

    public Task<List<StudentParent>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default) =>
        DbSet
            .Include(x => x.ParentUser)
            .Where(x => x.StudentId == studentId)
            .OrderBy(x => x.ParentUser.DisplayName)
            .ToListAsync(ct);
}
```

The global query filter on `SchoolId` applies to all these queries automatically — no manual tenant clause.

#### `AppDbContext`

Add the DbSet:

```csharp
public DbSet<StudentParent> StudentParents => Set<StudentParent>();
```

#### `DependencyInjection.cs` (Infrastructure)

Add to `AddInfrastructure`:

```csharp
services.AddScoped<IStudentParentRepository, StudentParentRepository>();
```

### WebApi (`SchoolMgmt.WebApi`)

#### `StudentParentsController`

A dedicated controller for the student sub-resource (keeps `StudentsController` focused on the student aggregate). Route-templated on the parent student id; Admin-only; state-mutating operations are POST/DELETE (GET is read-only per the rules).

```csharp
[ApiController]
[Route("api/students/{studentId:guid}")]
[Authorize(Roles = "Admin")]
public class StudentParentsController(ParentAccountService service) : ControllerBase
{
    [HttpPost("parent-login")]
    public async Task<IActionResult> CreateParentLogin(Guid studentId, CreateParentLoginRequest request, CancellationToken ct)
    {
        var result = await service.CreateParentLoginAsync(studentId, request, ct);
        return Ok(result);
    }

    [HttpGet("parents")]
    public async Task<IActionResult> GetParents(Guid studentId, CancellationToken ct)
    {
        var parents = await service.GetParentsForStudentAsync(studentId, ct);
        return Ok(parents);
    }

    [HttpDelete("parents/{parentUserId:guid}")]
    public async Task<IActionResult> RemoveParentLink(Guid studentId, Guid parentUserId, CancellationToken ct)
    {
        await service.RemoveParentLinkAsync(studentId, parentUserId, ct);
        return NoContent();
    }
}
```

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `POST` | `/api/students/{studentId}/parent-login` | `CreateParentLoginAsync` | 200 — `ParentLoginResultDto` | 404 if student unknown; 400 if `GuardianEmail` blank; 409 if email owned by a non-Parent user |
| `GET` | `/api/students/{studentId}/parents` | `GetParentsForStudentAsync` | 200 — `List<ParentAccountDto>` | 404 if student unknown |
| `DELETE` | `/api/students/{studentId}/parents/{parentUserId}` | `RemoveParentLinkAsync` | 204 | 404 if link not found; removes link only, not the User |

`POST` returns 200 (not 201) — the operation is create-or-reuse-or-noop and does not always mint a new addressable resource, and there is no single canonical `GET` URL for a "parent login" result to point a `Location` header at.

No `DomainExceptionFilter` changes needed — `NotFoundException` (→404), `ConflictException` (→409), and `DomainException` (→400) are already mapped by the existing filter.

## Project Structure

New and modified files:

```
backend/
  SchoolMgmt.Domain/
    Entities/
      StudentParent.cs                              # new

  SchoolMgmt.Application/
    Interfaces/
      IUserRepository.cs                            # modified — add FindByEmailInTenantAsync
    ParentAccounts/
      IStudentParentRepository.cs                   # new
      ParentAccountService.cs                       # new
      Dtos/
        CreateParentLoginRequest.cs                 # new
        ParentLoginResultDto.cs                     # new
        ParentAccountDto.cs                         # new
      Validators/
        CreateParentLoginRequestValidator.cs        # new
    DependencyInjection.cs                          # add ParentAccountService registration

  SchoolMgmt.Infrastructure/
    Persistence/
      AppDbContext.cs                               # add DbSet<StudentParent>
      Configurations/
        StudentParentConfiguration.cs               # new
      Repositories/
        UserRepository.cs                           # add FindByEmailInTenantAsync
        StudentParentRepository.cs                  # new
      Migrations/
        <AddStudentParents>                         # new — EF Core generated
    DependencyInjection.cs                          # add IStudentParentRepository registration

  SchoolMgmt.WebApi/
    Controllers/
      StudentParentsController.cs                   # new

tests/
  SchoolMgmt.Infrastructure.Tests/
    ParentAccounts/
      ParentAccountServiceTests.cs                  # new — hand-written fakes
  SchoolMgmt.IntegrationTests/
    ParentAccounts/
      ParentLinkTests.cs                            # new — real Postgres via Testcontainers
```

## Code Style

Follows [.claude/rules/backend.md](../.claude/rules/backend.md): thin controller → one `ParentAccountService` method per action; no MediatR; repositories only touch the `DbSet` (never `SaveChanges`/transactions); `IUnitOfWork.SaveChangesAsync()` called once at the end of each use case; interfaces defined in Application, implemented in Infrastructure; `Scoped` lifetime; GET endpoints read-only.

## Commands

```
Build:          dotnet build SchoolMgmt.slnx
Test:           dotnet test SchoolMgmt.slnx
Add migration:  dotnet ef migrations add AddStudentParents --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
Apply locally:  dotnet ef database update --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

## Database Schema Additions

### `StudentParents`

| Column | PG Type | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | NOT NULL | PK | — | Surrogate primary key |
| `SchoolId` | `uuid` | — | NOT NULL | — | — | Tenant scope (auto-stamped) |
| `StudentId` | `uuid` | — | NOT NULL | FK → `Students.Id` | `OnDelete Restrict`; UNIQUE with `UserId` | Linked student |
| `UserId` | `uuid` | — | NOT NULL | FK → `Users.Id` | `OnDelete Restrict`; UNIQUE with `StudentId` | Linked Parent-role user |
| `CreatedAt` | `timestamptz` | — | NOT NULL | — | — | Auto-stamped |
| `UpdatedAt` | `timestamptz` | — | NULL | — | — | Auto-stamped on modification |

Composite unique index `(StudentId, UserId)` enforces link idempotency at the DB level.

## Testing Strategy

### Unit (`SchoolMgmt.Infrastructure.Tests` — hand-written fakes, no mocking library)

`ParentAccountService` carries the real branching logic, so it gets focused unit tests with fake repositories and a fake `IPasswordHasher`:

- **Create — new account:** student with a `GuardianEmail`, no existing user → a Parent `User` is added with `Role=Parent`, `IsActive=true`, `DisplayName` = `GuardianName` (falls back to email when name is blank), `PasswordHash` = hashed temp password; a link is added; result `AccountCreated=true, LinkCreated=true`.
- **Create — reuse existing Parent:** a Parent user with that email already exists → no new user added, its `PasswordHash` is left untouched, link created; result `AccountCreated=false, LinkCreated=true`.
- **Create — idempotent:** existing Parent user **and** existing link → nothing added, `SaveChangesAsync` not required to change anything; result `AccountCreated=false, LinkCreated=false`.
- **Create — email owned by non-Parent (e.g. Teacher/Admin):** → `ConflictException`; no user and no link added.
- **Create — student has blank/null `GuardianEmail`:** → `DomainException`; nothing added.
- **Create — unknown student id:** → `NotFoundException`.
- **Create — email lookup is tenant-scoped:** service calls `FindByEmailInTenantAsync` (not `GetByEmailAsync`) — verified via the fake.
- **Remove — existing link:** link removed, `SaveChangesAsync` called; the Parent `User` is **not** removed (fake user repo records no `Remove`).
- **Remove — unknown link:** → `NotFoundException`.
- **GetParents — unknown student:** → `NotFoundException`.

### Integration (`SchoolMgmt.IntegrationTests` — real Postgres via Testcontainers, authenticated as demo Admin)

- `POST /api/students/{id}/parent-login` for a student with a guardian email → 200; body has `accountCreated=true, linkCreated=true`; a `Parent` user row now exists with that email.
- The created Parent can **log in**: `POST /api/auth/login` with the guardian email + the temporary password → 200 with auth cookies (proves the runtime-created user is a valid login).
- Two children of the same guardian email: `parent-login` on the second student → 200 with `accountCreated=false, linkCreated=true`; `GET .../parents` on both students returns the same `parentUserId`.
- Calling `parent-login` twice for the same student → second call returns `accountCreated=false, linkCreated=false`; `GET .../parents` still lists exactly one parent.
- `POST .../parent-login` for a student whose `GuardianEmail` is null/blank → 400.
- `POST .../parent-login` where the email matches an existing **Teacher/Admin** account → 409; no link created.
- `POST .../parent-login` with a temp password shorter than 8 chars → 400 (FluentValidation).
- `GET /api/students/{id}/parents` → 200 list with `email`, `displayName`, `accountCreatedAt`; unknown student → 404.
- `DELETE /api/students/{id}/parents/{parentUserId}` → 204; the link is gone from `GET .../parents`, but the Parent `User` row still exists (and, if linked to another child, still appears under that child).
- `DELETE` for a non-existent link → 404.
- **Auth:** every endpoint → 401 unauthenticated; → 403 for a Teacher/Parent role.

## Boundaries

- **Always:** run `dotnet test SchoolMgmt.slnx` before considering a task done; hash the temporary password via `IPasswordHasher` before storing (never store plaintext); create the Parent user and the `StudentParent` link in a single `IUnitOfWork.SaveChangesAsync()`; use `FindByEmailInTenantAsync` (tenant-scoped) for the collision/reuse check — never the pre-auth `GetByEmailAsync`; keep `GET .../parents` side-effect-free.
- **Ask first:** overwriting/resetting an existing Parent account's password on the reuse path (this spec deliberately does not); restricting `parent-login` based on the student's `EnrollmentStatus` (not currently restricted); returning the temporary password in the response body; adding a parent-facing read endpoint (that's a separate spec).
- **Never:** delete a Parent `User` when removing a link (remove the `StudentParent` row only); create a user with any role other than `Parent` through this feature; repurpose a non-Parent account as a parent login (must 409); call `SaveChangesAsync`/transaction methods from inside a repository; make `parent-login` or the link-delete a GET; bypass the tenant query filter anywhere in this feature.

## Success Criteria

- `StudentParent` entity exists (BaseEntity + ITenantScoped); migration creates the `StudentParents` table with the `(StudentId, UserId)` unique index and both `OnDelete Restrict` FKs.
- `POST /api/students/{id}/parent-login` creates a `Parent` user + link and returns `accountCreated`/`linkCreated` flags; the created parent can log in via the existing `POST /api/auth/login`.
- The same guardian email across two children yields one shared Parent account (second call reports `accountCreated=false`).
- The operation is idempotent — a repeat call creates nothing and reports both flags `false`.
- Blank `GuardianEmail` → 400; email owned by a non-Parent account → 409.
- `DELETE .../parents/{parentUserId}` removes only the link (204); the `User` row survives.
- All endpoints return 401 unauthenticated and 403 for non-Admin roles.
- All unit and integration tests listed above pass under `dotnet test SchoolMgmt.slnx`.

## Open Questions

None. The four design decisions from the spec-clarification step are resolved: backend-only scope; non-Parent email collision returns 409; Admin-side only (no parent-facing endpoints); no force-password-change flag in v1.
