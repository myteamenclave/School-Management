# Spec: Implement Class / Section Structure

## Related docs & specs

- [docs/ideas/02-class-section-structure.md](../docs/ideas/02-class-section-structure.md) — idea doc: problem statement, persistent model rationale, delete-requires-empty rule, scope and not-doing list
- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — source idea: class/section structure is Admin-owned, foundational, prerequisite for student enrollment
- [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md) — provides `BaseEntity`, `ITenantScoped`, `IRepository<TEntity>`, `IUnitOfWork`, `AppDbContext` — all used without modification
- [specs/02-implement-auth.md](02-implement-auth.md) — provides `[Authorize(Roles = "Admin")]` and RBAC infrastructure; all endpoints in this spec are Admin-only
- [specs/03-implement-academic-year-term-configuration.md](03-implement-academic-year-term-configuration.md) — establishes `DomainException` and `DomainExceptionFilter`; this spec introduces `ConflictException` and `NotFoundException` following the same filter convention. Note: spec #3 should use these same types when implemented.
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller/Application-service pattern, DI conventions, GET-must-be-read-only, Repository/UnitOfWork rules

## Objective

Implement a two-level structural catalog — `Grade` (academic level, e.g. "Grade 5") containing a configurable number of `Section` entities (e.g. "A", "B", "C") — as persistent, tenant-scoped entities. Admin can create grades and sections, rename them, and delete them (grades only when empty, sections freely). Every downstream module (student enrollment, attendance, gradebook, fees) will reference `Section` as its structural anchor.

**Out of scope for this spec:** teacher/homeroom assignment to sections (Staff CRUD not yet built); per-academic-year section reconfiguration (persistent model chosen); section capacity; any frontend UI.

## Tech Stack

- .NET 8.0, C# — same solution structure as specs #1–#3
- EF Core (Npgsql) — two new tables (`Grades`, `Sections`)
- FluentValidation — request validators registered via the existing global `IAsyncActionFilter` (already wired from spec #2; no new infra needed)
- xUnit + `WebApplicationFactory` + Testcontainers (Postgres) — same test setup as prior specs

## Design

### Domain (`SchoolMgmt.Domain`)

`ConflictException` (→ 409) and `NotFoundException` (→ 404) already exist in `Domain/Common/` and are already handled by the existing `DomainExceptionFilter` — no new exceptions or filters needed. `GradeService` uses them directly.

#### `Grade`

```csharp
namespace SchoolMgmt.Domain.Entities;

public class Grade : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    private readonly List<Section> _sections = [];
    public IReadOnlyList<Section> Sections => _sections.AsReadOnly();

    public void EnsureNoSections()
    {
        if (_sections.Count > 0)
            throw new DomainException("Cannot delete a grade that still has sections. Delete all sections first.");
    }
}
```

**Design notes:**
- No factory method — Grade has no invariant requiring controlled construction (unlike `AcademicYear`'s 2-semester invariant). Direct instantiation via object initializer is fine.
- `_sections` is a backing field; EF Core maps it via `HasField("_sections")` in `GradeConfiguration`.
- `EnsureNoSections()` enforces the delete guard at the domain level — even if the service forgets the check, the entity refuses.

#### `Section`

```csharp
namespace SchoolMgmt.Domain.Entities;

public class Section : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}
```

`Section` is `ITenantScoped` — carries `SchoolId` directly per the architecture rule. `AppDbContext.SaveChangesAsync` auto-stamps it from the tenant context.

### Application layer (`SchoolMgmt.Application`)

#### `IGradeRepository`

```csharp
namespace SchoolMgmt.Application.Grades;

public interface IGradeRepository : IRepository<Grade>
{
    Task<List<Grade>> GetAllWithSectionsAsync(CancellationToken ct = default);
    Task<Grade?> GetWithSectionsAsync(Guid id, CancellationToken ct = default);
    Task<bool> GradeNameExistsAsync(string name, CancellationToken ct = default);
    Task<Section?> GetSectionAsync(Guid gradeId, Guid sectionId, CancellationToken ct = default);
    Task<bool> SectionNameExistsInGradeAsync(Guid gradeId, string name, CancellationToken ct = default);
    Task AddSectionAsync(Section section, CancellationToken ct = default);
    void RemoveSection(Section section);
}
```

`Section` does not get its own `ISectionRepository` — it is always accessed through the grade's repository, consistent with how `Semester` is handled in spec #3 (via `context.Set<Semester>()` internally in `AcademicYearRepository`).

#### DTOs

```csharp
namespace SchoolMgmt.Application.Grades;

public record SectionDto(Guid Id, Guid GradeId, string Name);

public record GradeDto(Guid Id, string Name, int DisplayOrder, List<SectionDto> Sections);

public record CreateGradeRequest(string Name, int DisplayOrder);
public record UpdateGradeRequest(string Name, int DisplayOrder);
public record CreateSectionRequest(string Name);
public record UpdateSectionRequest(string Name);
```

#### Validators (FluentValidation)

```csharp
// CreateGradeRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

// UpdateGradeRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

// CreateSectionRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(50);

// UpdateSectionRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
```

#### `GradeService`

One service for both Grade and Section operations, per the "one service per feature area" rule. All section operations are in the context of a grade.

**Mapping convention:** private static `ToDto(Grade)` and `ToDto(Section)` helpers in the service; no AutoMapper.

```
CreateGradeAsync(CreateGradeRequest, CancellationToken) → GradeDto
  - GradeNameExistsAsync(request.Name) → throw ConflictException("A grade named '{name}' already exists.") if true
  - new Grade { Name = request.Name, DisplayOrder = request.DisplayOrder }
  - repository.AddAsync(grade)
  - unitOfWork.SaveChangesAsync()
  - Return GradeDto (Sections = [])

GetAllGradesAsync(CancellationToken) → List<GradeDto>
  - GetAllWithSectionsAsync() — ordered by DisplayOrder ascending
  - Map each to GradeDto

GetGradeByIdAsync(Guid id, CancellationToken) → GradeDto
  - GetWithSectionsAsync(id) → throw NotFoundException("Grade not found.") if null
  - Map to GradeDto

UpdateGradeAsync(Guid id, UpdateGradeRequest, CancellationToken) → GradeDto
  - GetWithSectionsAsync(id) → throw NotFoundException("Grade not found.") if null
  - If request.Name != grade.Name: GradeNameExistsAsync(request.Name) → throw ConflictException if true
  - grade.Name = request.Name; grade.DisplayOrder = request.DisplayOrder
  - repository.Update(grade)
  - unitOfWork.SaveChangesAsync()
  - Return GradeDto

DeleteGradeAsync(Guid id, CancellationToken) → void
  - GetWithSectionsAsync(id) → throw NotFoundException("Grade not found.") if null
  - grade.EnsureNoSections() → throws DomainException if sections exist
  - repository.Remove(grade)
  - unitOfWork.SaveChangesAsync()

AddSectionAsync(Guid gradeId, CreateSectionRequest, CancellationToken) → SectionDto
  - GetByIdAsync(gradeId) → throw NotFoundException("Grade not found.") if null
  - SectionNameExistsInGradeAsync(gradeId, request.Name) → throw ConflictException("A section named '{name}' already exists in this grade.") if true
  - new Section { GradeId = gradeId, Name = request.Name }
  - repository.AddSectionAsync(section)
  - unitOfWork.SaveChangesAsync()
  - Return SectionDto

UpdateSectionAsync(Guid gradeId, Guid sectionId, UpdateSectionRequest, CancellationToken) → SectionDto
  - GetSectionAsync(gradeId, sectionId) → throw NotFoundException("Section not found.") if null
  - If request.Name != section.Name: SectionNameExistsInGradeAsync(gradeId, request.Name) → throw ConflictException if true
  - section.Name = request.Name
  - unitOfWork.SaveChangesAsync() — entity is tracked; change tracker detects the modification; SaveChangesAsync stamps UpdatedAt
  - Return SectionDto

DeleteSectionAsync(Guid gradeId, Guid sectionId, CancellationToken) → void
  - GetSectionAsync(gradeId, sectionId) → throw NotFoundException("Section not found.") if null
  - repository.RemoveSection(section)
  - unitOfWork.SaveChangesAsync()
```

#### `DependencyInjection.cs` (Application)

Append to the existing `AddApplication` method:

```csharp
services.AddScoped<GradeService>();
```

Validators are registered via the existing `services.AddValidatorsFromAssembly(...)` call.

### Infrastructure (`SchoolMgmt.Infrastructure`)

#### `GradeConfiguration`

```csharp
// Table: Grades
builder.ToTable("Grades");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
builder.Property(x => x.DisplayOrder).IsRequired();
builder.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();

// Backing field for Sections navigation
builder.Navigation(x => x.Sections).HasField("_sections");
```

#### `SectionConfiguration`

```csharp
// Table: Sections
builder.ToTable("Sections");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
builder.HasIndex(x => new { x.GradeId, x.Name }).IsUnique();

// FK: ON DELETE RESTRICT — grade must be emptied before deletion
builder.HasOne(x => x.Grade)
    .WithMany("_sections")
    .HasForeignKey(x => x.GradeId)
    .OnDelete(DeleteBehavior.Restrict);
```

`DeleteBehavior.Restrict` provides a DB-level safety net that mirrors the domain guard: even if `EnsureNoSections()` were somehow bypassed in application code, the FK constraint rejects the delete.

#### `GradeRepository`

```csharp
internal sealed class GradeRepository(AppDbContext context)
    : Repository<Grade>(context), IGradeRepository
{
    public Task<List<Grade>> GetAllWithSectionsAsync(CancellationToken ct = default) =>
        DbSet.Include(g => g.Sections).OrderBy(g => g.DisplayOrder).ToListAsync(ct);

    public Task<Grade?> GetWithSectionsAsync(Guid id, CancellationToken ct = default) =>
        DbSet.Include(g => g.Sections).FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<bool> GradeNameExistsAsync(string name, CancellationToken ct = default) =>
        DbSet.AnyAsync(g => g.Name == name, ct);

    public Task<Section?> GetSectionAsync(Guid gradeId, Guid sectionId, CancellationToken ct = default) =>
        context.Set<Section>().FirstOrDefaultAsync(s => s.GradeId == gradeId && s.Id == sectionId, ct);

    public Task<bool> SectionNameExistsInGradeAsync(Guid gradeId, string name, CancellationToken ct = default) =>
        context.Set<Section>().AnyAsync(s => s.GradeId == gradeId && s.Name == name, ct);

    public Task AddSectionAsync(Section section, CancellationToken ct = default)
    {
        context.Set<Section>().Add(section);
        return Task.CompletedTask;
    }

    public void RemoveSection(Section section) => context.Set<Section>().Remove(section);
}
```

`context.Set<Section>()` is used directly — the same pattern as spec #3's `AcademicYearRepository` accessing `Semester`. No standalone `ISectionRepository` is introduced.

#### `DependencyInjection.cs` (Infrastructure)

Add to `AddInfrastructure`:

```csharp
services.AddScoped<IGradeRepository, GradeRepository>();
```

### WebApi (`SchoolMgmt.WebApi`)

#### `GradesController`

All endpoints require `[Authorize(Roles = "Admin")]`. All state-mutating operations are POST/PUT/DELETE — no GET side effects.

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `GET` | `/api/grades` | `GetAllGradesAsync` | 200 | Ordered by DisplayOrder; includes sections |
| `GET` | `/api/grades/{id}` | `GetGradeByIdAsync` | 200 | 404 if not found |
| `POST` | `/api/grades` | `CreateGradeAsync` | 201 + Location header | 409 if name duplicate |
| `PUT` | `/api/grades/{id}` | `UpdateGradeAsync` | 200 | 404 if not found; 409 if name duplicate |
| `DELETE` | `/api/grades/{id}` | `DeleteGradeAsync` | 204 | 404 if not found; 400 if has sections |
| `POST` | `/api/grades/{gradeId}/sections` | `AddSectionAsync` | 201 + Location header | 404 if grade not found; 409 if name duplicate in grade |
| `PUT` | `/api/grades/{gradeId}/sections/{sectionId}` | `UpdateSectionAsync` | 200 | 404 if not found; 409 if name duplicate in grade |
| `DELETE` | `/api/grades/{gradeId}/sections/{sectionId}` | `DeleteSectionAsync` | 204 | 404 if not found |

## Project Structure

New files introduced by this spec:

```
backend/
  SchoolMgmt.Domain/
    Common/
      ConflictException.cs              # already exists — no change
      NotFoundException.cs              # already exists — no change
    Entities/
      Grade.cs                          # new
      Section.cs                        # new

  SchoolMgmt.Application/
    Grades/
      IGradeRepository.cs               # new
      GradeService.cs                   # new
      Dtos/
        GradeDto.cs                     # new
        SectionDto.cs                   # new
        CreateGradeRequest.cs           # new
        UpdateGradeRequest.cs           # new
        CreateSectionRequest.cs         # new
        UpdateSectionRequest.cs         # new
      Validators/
        CreateGradeRequestValidator.cs  # new
        UpdateGradeRequestValidator.cs  # new
        CreateSectionRequestValidator.cs # new
        UpdateSectionRequestValidator.cs # new
    DependencyInjection.cs              # add GradeService registration

  SchoolMgmt.Infrastructure/
    Persistence/
      Configurations/
        GradeConfiguration.cs          # new
        SectionConfiguration.cs        # new
      Repositories/
        GradeRepository.cs             # new
      Migrations/
        <AddGradesAndSections>         # new — EF Core generated
    DependencyInjection.cs             # add IGradeRepository registration

  SchoolMgmt.WebApi/
    Controllers/
      GradesController.cs              # new
    Filters/
      DomainExceptionFilter.cs         # already handles ConflictException → 409 and NotFoundException → 404 — no change
    Program.cs                         # no changes needed — DomainExceptionFilter already registered

tests/
  SchoolMgmt.Domain.Tests/
    GradeTests.cs                      # new — pure domain logic, no DB
  SchoolMgmt.IntegrationTests/
    GradesControllerTests.cs           # new — real Postgres via Testcontainers
```

## Commands

```
Build:          dotnet build SchoolMgmt.slnx
Test:           dotnet test SchoolMgmt.slnx
Add migration:  dotnet ef migrations add AddGradesAndSections --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
Apply locally:  dotnet ef database update --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

## Database Schema Additions

### `Grades`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | — |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope |
| `Name` | `varchar` | 100 | — | NOT NULL | — | UNIQUE with `SchoolId` | e.g. "Grade 5" |
| `DisplayOrder` | `int` | — | — | NOT NULL | — | — | Admin-set ordering |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

### `Sections`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | — |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope |
| `GradeId` | `uuid` | — | — | NOT NULL | FK → `Grades.Id` | ON DELETE RESTRICT | — |
| `Name` | `varchar` | 50 | — | NOT NULL | — | UNIQUE with `GradeId` | e.g. "A", "B" |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

`ON DELETE RESTRICT` on `GradeId` mirrors the `EnsureNoSections()` domain guard at the database level — a belt-and-suspenders defense so that even if application code bypasses the domain check, the FK constraint rejects the delete.

## Testing Strategy

### Unit tests (`SchoolMgmt.Domain.Tests`)

Pure domain logic — no DB, no fakes needed:

- `Grade.EnsureNoSections` throws `DomainException` when the grade has one or more sections
- `Grade.EnsureNoSections` does not throw when the grade has no sections

### Integration tests (`SchoolMgmt.IntegrationTests`)

Against real Postgres (Testcontainers), authenticated as the demo Admin:

**Grades:**
- `POST /api/grades` — creates a grade; 201 returned; response body contains `id`, `name`, `displayOrder`, empty `sections` array
- `POST /api/grades` with a duplicate name — returns 409
- `GET /api/grades` — returns the created grades in `displayOrder` order, each with their sections
- `GET /api/grades/{id}` — returns 404 for an unknown id
- `PUT /api/grades/{id}` — updates name and display order; 200 returned; re-fetching confirms the change
- `PUT /api/grades/{id}` with a name already used by another grade — returns 409
- `DELETE /api/grades/{id}` with no sections — returns 204; re-fetching returns 404
- `DELETE /api/grades/{id}` that has at least one section — returns 400

**Sections:**
- `POST /api/grades/{gradeId}/sections` — creates a section; 201 returned; re-fetching the grade includes the new section
- `POST /api/grades/{gradeId}/sections` with a duplicate name in the same grade — returns 409
- `POST /api/grades/{gradeId}/sections` with a name identical to a section in a *different* grade — returns 201 (name uniqueness is per-grade, not global)
- `PUT /api/grades/{gradeId}/sections/{sectionId}` — updates section name; 200 returned; re-fetching the grade reflects the update
- `DELETE /api/grades/{gradeId}/sections/{sectionId}` — returns 204; re-fetching the grade no longer includes the section
- `DELETE /api/grades/{gradeId}/sections/{sectionId}` with an unknown section id — returns 404

**Auth:**
- Unauthenticated request to any endpoint — returns 401
- Request with Teacher role — returns 403

## Boundaries

- **Always:** call `dotnet test SchoolMgmt.slnx` before considering any task in this spec done; follow the DI/layering rules in `.claude/rules/backend.md`; keep GET endpoints side-effect-free.
- **Ask first:** adding a teacher/homeroom FK to `Section` (Staff entity not yet built — requires coordination with the Staff CRUD spec); changing the `GradeId` unique constraint on `Sections` to include or exclude `SchoolId` (affects the DB-level uniqueness guarantee); making section deletion cascade from grade deletion instead of requiring empty grades.
- **Never:** add a GET endpoint with side effects; call `SaveChanges`/`SaveChangesAsync` or transaction methods from inside a repository implementation; add a standalone `ISectionRepository` (sections are always accessed through `IGradeRepository`); skip the `EnsureNoSections()` call before grade deletion.

## Success Criteria

- `Grade.EnsureNoSections()` throws `DomainException` when sections exist — verified by domain unit test.
- `POST /api/grades` creates a grade with an empty sections array — integration test passes.
- `DELETE /api/grades/{id}` on a grade with sections returns HTTP 400 — domain rule enforced at entity level, surfaced via `DomainExceptionFilter`.
- `DELETE /api/grades/{id}` on an empty grade succeeds (204) and the grade is gone — integration test passes.
- Same section name in two different grades is allowed; same section name in the same grade returns 409 — integration tests confirm per-grade uniqueness.
- `ConflictException` → 409 and `NotFoundException` → 404 via the existing `DomainExceptionFilter` — no new filter registration needed; these exception types are already handled.
- All endpoints return 401 for unauthenticated requests and 403 for non-Admin roles.
- All tests in `dotnet test SchoolMgmt.slnx` pass.

## Open Questions

None — all design decisions resolved in the idea doc and the ideation session.
