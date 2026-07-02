# Plan: Implement Teacher CRUD

Spec: [specs/06-implement-teacher-crud.md](../specs/06-implement-teacher-crud.md)
Idea doc: [docs/ideas/04-teacher-crud.md](../docs/ideas/04-teacher-crud.md)

## Context

Teachers are both Users (they log in) and school-profile records. This spec creates a `Teacher` entity with a FK to the existing `User` entity, adds `User.IsActive` (and an `AuthService.LoginAsync` check), and wires up Admin CRUD — all following the same patterns as the Student CRUD spec (#05).

Key differences from Student CRUD:
- `CreateTeacherAsync` runs a `BeginTransaction`/`Commit` because it must write two tables atomically (User + Teacher).
- `UpdateTeacherAsync` must sync `Teacher.IsActive` and `User.IsActive` in the same `SaveChangesAsync`.
- One migration covers both the new `Teachers` table and the new `IsActive` column on `Users`.

---

## Task 1 — Patch existing: `User.IsActive` + `AuthService` login check

**Modified files:**
- `backend/SchoolMgmt.Domain/Entities/User.cs` — add `public bool IsActive { get; set; } = true;`
- `backend/SchoolMgmt.Application/Auth/AuthService.cs` — after password check, add `if (!user.IsActive) return null;`

No new files. No new tests yet (regression guard is in Task 5 integration tests).

**Verify:** `dotnet build backend/SchoolMgmt.slnx` passes.

---

## Task 2 — Domain: `Teacher` entity

**New files:**
- `backend/SchoolMgmt.Domain/Entities/Teacher.cs`

```csharp
public class Teacher : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TeacherCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly JoiningDate { get; set; }
    public bool IsActive { get; set; } = true;
}
```

No domain unit tests — `Teacher` has no invariants or guards.

**Verify:** `dotnet build backend/SchoolMgmt.Domain` passes.

---

## Task 3 — Application layer

**New files:**
- `backend/SchoolMgmt.Application/Teachers/ITeacherRepository.cs`
- `backend/SchoolMgmt.Application/Teachers/TeacherService.cs`
- `backend/SchoolMgmt.Application/Teachers/TeacherOptions.cs`
- `backend/SchoolMgmt.Application/Teachers/Dtos/TeacherSummaryDto.cs`
- `backend/SchoolMgmt.Application/Teachers/Dtos/TeacherDto.cs`
- `backend/SchoolMgmt.Application/Teachers/Dtos/CreateTeacherRequest.cs`
- `backend/SchoolMgmt.Application/Teachers/Dtos/UpdateTeacherRequest.cs`
- `backend/SchoolMgmt.Application/Teachers/Validators/CreateTeacherRequestValidator.cs`
- `backend/SchoolMgmt.Application/Teachers/Validators/UpdateTeacherRequestValidator.cs`

**Modified files:**
- `backend/SchoolMgmt.Application/DependencyInjection.cs` — add `TeacherService` + `services.Configure<TeacherOptions>(...)`; add `IConfiguration configuration` parameter if not already present

**`ITeacherRepository`:**
```csharp
public interface ITeacherRepository : IRepository<Teacher>
{
    Task<(List<Teacher> Items, int TotalCount)> GetPagedAsync(bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<Teacher?> GetByIdWithUserAsync(Guid id, CancellationToken ct = default);
    Task<string> GetNextTeacherCodeAsync(int joiningYear, CancellationToken ct = default);
}
```

**`TeacherService` — `CreateTeacherAsync` algorithm:**
```
BeginTransactionAsync
try:
  user = new User { Email, PasswordHash = hash(password), DisplayName = "F L", Role = Teacher, IsActive = true }
  AddAsync(user) → SaveChangesAsync  // flush to get user.Id

  teacher = new Teacher { UserId = user.Id, FirstName, LastName, Phone, JoiningDate, IsActive = true }
  for attempt in 0.._maxRetries:
    teacher.TeacherCode = await GetNextTeacherCodeAsync(joiningYear)
    AddAsync(teacher) → try SaveChangesAsync
      success → break
      ConflictException (when attempt < max-1) → Detach(teacher)
  else → throw DomainException

  CommitAsync
  teacher.User = user
  return ToDto(teacher)
catch:
  RollbackAsync
  throw
```

**`UpdateTeacherAsync`:** load via `GetByIdWithUserAsync`; if `IsActive` changes, set both `teacher.IsActive` and `teacher.User.IsActive`, call `userRepository.Update(teacher.User)`.

**DTOs:**
- `TeacherSummaryDto(Id, TeacherCode, FirstName, LastName, Phone?, JoiningDate, IsActive, Email)`
- `TeacherDto(Id, TeacherCode, FirstName, LastName, Phone?, JoiningDate, IsActive, Email, UserId, CreatedAt, UpdatedAt?)`
- `CreateTeacherRequest(Email, Password, FirstName, LastName, Phone?, JoiningDate)`
- `UpdateTeacherRequest(FirstName, LastName, Phone?, JoiningDate, IsActive)` — no Email/Password

**Validators:**
- Create: Email (NotEmpty, MaxLength 256, EmailAddress), Password (NotEmpty, MinLength 8), FirstName/LastName (NotEmpty, MaxLength 100), Phone (MaxLength 20, When not null), JoiningDate (NotEmpty)
- Update: FirstName/LastName (NotEmpty, MaxLength 100), Phone (MaxLength 20, When not null), JoiningDate (NotEmpty)

**Reuse:** `PagedResult<T>` already exists in `Students/Dtos/PagedResult.cs` — import, don't duplicate.

**Verify:** `dotnet build backend/SchoolMgmt.Application` passes.

---

## Task 4 — Infrastructure layer

**New files:**
- `backend/SchoolMgmt.Infrastructure/Persistence/Configurations/TeacherConfiguration.cs`
- `backend/SchoolMgmt.Infrastructure/Persistence/Repositories/TeacherRepository.cs`

**Modified files:**
- `backend/SchoolMgmt.Infrastructure/Persistence/Configurations/UserConfiguration.cs` — add `builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);`
- `backend/SchoolMgmt.Infrastructure/Persistence/AppDbContext.cs` — add `public DbSet<Teacher> Teachers => Set<Teacher>();`
- `backend/SchoolMgmt.Infrastructure/DependencyInjection.cs` — add `services.AddScoped<ITeacherRepository, TeacherRepository>()`

**`TeacherConfiguration` key points:**
```csharp
builder.ToTable("Teachers");
builder.HasOne(t => t.User).WithOne().HasForeignKey<Teacher>(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
builder.HasIndex(x => new { x.SchoolId, x.TeacherCode }).IsUnique();
builder.Property(x => x.JoiningDate).IsRequired().HasColumnType("date");
builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
```

**`TeacherRepository`:** mirrors `StudentRepository` pattern — `GetPagedAsync` uses `Include(t => t.User)`, defaults to `isActive == true`. `GetByIdWithUserAsync` uses `Include` + `FirstOrDefaultAsync`. `GetNextTeacherCodeAsync` uses `MAX(TeacherCode)` with `{year}-` prefix.

**Generate migration:**
```
dotnet ef migrations add AddTeachersWithUserIsActive --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

**Verify:** Migration file shows `AddColumn<bool>("IsActive", ...)` on `Users` and `CreateTable("Teachers")`. `dotnet build backend/SchoolMgmt.Infrastructure` passes.

---

## Task 5 — WebApi layer

**New files:**
- `backend/SchoolMgmt.WebApi/Controllers/TeachersController.cs`

**No Program.cs changes** — `DomainExceptionFilter` and `ValidationFilter` are already registered globally.

**Controller:** `[ApiController][Route("api/teachers")][Authorize(Roles = "Admin")]`

| Action | HTTP | Route | Success | Notes |
|---|---|---|---|---|
| `Create` | POST | `` | 201 + `Location: /api/teachers/{id}` | `{id}` = Teacher.Id |
| `GetAll` | GET | `` | 200 `PagedResult<TeacherSummaryDto>` | `?isActive=&page=&pageSize=`; pageSize clamped to 100 |
| `GetById` | GET | `{id}` | 200 `TeacherDto` | 404 if not found |
| `Update` | PUT | `{id}` | 200 `TeacherDto` | 404 if not found |

**Verify:** `dotnet build backend/SchoolMgmt.WebApi` passes.

---

## Task 6 — Integration tests

**New file:**
- `backend/tests/SchoolMgmt.IntegrationTests/Teachers/TeachersControllerTests.cs`

Uses `[Collection(IntegrationTestCollection.Name)]` + `PostgresContainerFixture`. Authenticates as demo Admin.

**Tests:**

Create:
1. Valid data → 201, `teacherCode` matches `YYYY-NNNNNN`, `isActive = true`, email present
2. Two sequential creates same joining year → codes increment (`2026-000001`, `2026-000002`)
3. Create with 2025 joining year → code starts with `2025-`
4. Duplicate email → 409
5. Missing `firstName` → 400
6. Invalid email → 400
7. Password shorter than 8 chars → 400

Read:
8. `GET /api/teachers` (no params) → only active teachers
9. `GET /api/teachers?isActive=false` → only inactive teachers
10. `GET /api/teachers?pageSize=200` → clamped to 100
11. `GET /api/teachers/{id}` → 200 with `email`, `userId`
12. `GET /api/teachers/{unknownId}` → 404

Update:
13. Updated name → 200; `updatedAt` is set
14. `isActive = false` → teacher absent from default GET list
15. `isActive = false` → subsequent `POST /api/auth/login` for that email → 401 (login disabled)
16. `teacherCode` in response unchanged after update
17. `email` in response unchanged after update
18. Unknown id → 404

No delete:
19. `DELETE /api/teachers/{id}` → 405

Auth:
20. Unauthenticated → 401
21. Teacher role → 403

Auth regression:
22. Demo Admin login still works after migration (IsActive defaults to true)

**Verify:** `dotnet test backend/SchoolMgmt.slnx` — all tests pass (new + existing).

---

## Task 7 — Catalog update

Update `.claude/catalog/backend.md` with all new/modified public types:
- `User` (modified — IsActive)
- `Teacher` (new entity)
- `ITeacherRepository`, `TeacherRepository`, `TeacherConfiguration`, `TeacherOptions`, `TeacherService`, DTOs, validators
- `AppDbContext` DbSet addition
- Migration `AddTeachersWithUserIsActive`
- `TeachersController`

---

## Dependency order

```
Task 1: Patch User + AuthService
    ↓
Task 2: Teacher entity
    ↓
Task 3: Application layer
    ↓
Task 4: Infrastructure + migration
    ↓
Task 5: WebApi controller
    ↓
Task 6: Integration tests
    ↓
Task 7: Catalog update
```
