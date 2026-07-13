# Spec: Implement Class / Section Assignment

## Related docs & specs

- [docs/ideas/09-class-section-assignment.md](../docs/ideas/09-class-section-assignment.md) — idea doc: problem statement, two-entity model, assignment UI directions, fee invoicing connection
- [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md) — `BaseEntity`, `ITenantScoped`, `IRepository<TEntity>`, `IUnitOfWork`, `AppDbContext`; used as-is
- [specs/03-implement-academic-year-term-configuration.md](03-implement-academic-year-term-configuration.md) — `AcademicYear` entity; both new entities reference it
- [specs/04-implement-class-section-structure.md](04-implement-class-section-structure.md) — `Grade` + `Section` entities, `IGradeRepository`; this spec extends `IGradeRepository` with a section-by-id lookup
- [specs/05-implement-student-crud.md](05-implement-student-crud.md) — `Student` entity, `IStudentRepository`
- [specs/07-B-implement-teacher-crud-frontend.md](07-B-implement-teacher-crud-frontend.md) — `Teacher` entity, `ITeacherRepository`; structural reference
- [specs/07-implement-subject-management.md](07-implement-subject-management.md) — `Subject` entity, `ISubjectRepository`; structural reference
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller / Application-service pattern, GET-must-be-read-only, Repository / UnitOfWork rules

## Objective

Implement Admin-only CRUD for two assignment junction entities:

- **`StudentSectionEnrollment`** — places one student in one section for one academic year. One active placement per student per year; Admin can update the section at any time (covers both intra-grade transfers and grade changes). This record is the query anchor the fee invoicing grade-broadcast will use: `StudentSectionEnrollment JOIN Section WHERE Section.GradeId = targetGrade AND AcademicYearId = targetYear`.

- **`TeacherSectionSubject`** — assigns a teacher to teach a subject in a specific section for one academic year. One teacher per subject-section-year (no co-teaching). A teacher can have multiple rows across different subject-section combinations.

**Out of scope for this spec:** homeroom/class teacher designation, transfer history, bulk enrollment from CSV, timetable/scheduling, frontend UI.

## Tech Stack

- .NET 8.0, C# — same solution as all prior specs
- EF Core (Npgsql) — two new tables (`StudentSectionEnrollments`, `TeacherSectionSubjects`)
- FluentValidation — request validators via the existing global `IAsyncActionFilter`
- xUnit + `WebApplicationFactory` + Testcontainers (Postgres) — same integration test setup

---

## Design

### Domain — `SchoolMgmt.Domain`

#### New entity: `StudentSectionEnrollment`

```csharp
// SchoolMgmt.Domain/Entities/StudentSectionEnrollment.cs
namespace SchoolMgmt.Domain.Entities;

public class StudentSectionEnrollment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}
```

#### New entity: `TeacherSectionSubject`

```csharp
// SchoolMgmt.Domain/Entities/TeacherSectionSubject.cs
namespace SchoolMgmt.Domain.Entities;

public class TeacherSectionSubject : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public Guid SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}
```

Neither entity has domain behavior beyond data storage.

---

### Application layer — `SchoolMgmt.Application`

#### Modified: `IGradeRepository`

Add one method to support section lookup by ID alone (needed by both services to validate and load section + grade context):

```csharp
Task<Section?> GetSectionByIdAsync(Guid sectionId, CancellationToken ct = default);
```

#### New: `IStudentSectionEnrollmentRepository`

```csharp
// SchoolMgmt.Application/Enrollments/IStudentSectionEnrollmentRepository.cs
namespace SchoolMgmt.Application.Enrollments;

public interface IStudentSectionEnrollmentRepository : IRepository<StudentSectionEnrollment>
{
    Task<List<StudentSectionEnrollment>> GetBySectionAndYearAsync(
        Guid sectionId, Guid academicYearId, CancellationToken ct = default);

    Task<StudentSectionEnrollment?> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<StudentSectionEnrollment?> GetByIdWithDetailsAsync(
        Guid id, CancellationToken ct = default);
}
```

- `GetBySectionAndYearAsync`: returns all enrollments for a section/year, each including `Student`, `Section` (with `Grade`), and `AcademicYear`; ordered by `Student.LastName`, then `Student.FirstName`.
- `GetByStudentAndYearAsync`: returns the single enrollment for a student in a given year, or null if none. Used for duplicate-check before creating.
- `GetByIdWithDetailsAsync`: loads one enrollment with all navigation properties. Used for transfer (PUT) and response after update.

#### New: `ITeacherSectionSubjectRepository`

```csharp
// SchoolMgmt.Application/TeacherAssignments/ITeacherSectionSubjectRepository.cs
namespace SchoolMgmt.Application.TeacherAssignments;

public interface ITeacherSectionSubjectRepository : IRepository<TeacherSectionSubject>
{
    Task<List<TeacherSectionSubject>> GetByTeacherAndYearAsync(
        Guid teacherId, Guid academicYearId, CancellationToken ct = default);

    Task<TeacherSectionSubject?> GetBySubjectSectionAndYearAsync(
        Guid subjectId, Guid sectionId, Guid academicYearId, CancellationToken ct = default);
}
```

- `GetByTeacherAndYearAsync`: returns all subject-section slots for a teacher in a given year, each including `Subject`, `Section` (with `Grade`), and `AcademicYear`; ordered by `Section.Grade.DisplayOrder`, then `Section.Name`, then `Subject.Name`.
- `GetBySubjectSectionAndYearAsync`: uniqueness check — returns the existing row (or null) for a subject-section-year combination, regardless of teacher.

#### DTOs

```csharp
// SchoolMgmt.Application/Enrollments/Dtos/

public record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    string StudentCode,
    string StudentFirstName,
    string StudentLastName,
    Guid SectionId,
    string SectionName,
    Guid GradeId,
    string GradeName,
    Guid AcademicYearId,
    string AcademicYearName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record CreateEnrollmentRequest(
    Guid StudentId,
    Guid AcademicYearId
);

public record TransferEnrollmentRequest(
    Guid SectionId    // the new section to move the student into
);

// SchoolMgmt.Application/TeacherAssignments/Dtos/

public record TeacherAssignmentDto(
    Guid Id,
    Guid TeacherId,
    Guid SubjectId,
    string SubjectName,
    string SubjectCode,
    Guid SectionId,
    string SectionName,
    Guid GradeId,
    string GradeName,
    Guid AcademicYearId,
    string AcademicYearName,
    DateTimeOffset CreatedAt
);

public record CreateTeacherAssignmentRequest(
    Guid SubjectId,
    Guid SectionId,
    Guid AcademicYearId
);
```

#### Validators (FluentValidation)

```csharp
// CreateEnrollmentRequestValidator
RuleFor(x => x.StudentId).NotEmpty();
RuleFor(x => x.AcademicYearId).NotEmpty();

// TransferEnrollmentRequestValidator
RuleFor(x => x.SectionId).NotEmpty();

// CreateTeacherAssignmentRequestValidator
RuleFor(x => x.SubjectId).NotEmpty();
RuleFor(x => x.SectionId).NotEmpty();
RuleFor(x => x.AcademicYearId).NotEmpty();
```

Cross-entity business rules (duplicate check, FK existence) are validated in the service, not the validators.

#### `EnrollmentService`

Injects `IStudentSectionEnrollmentRepository`, `IStudentRepository`, `IGradeRepository`, `IAcademicYearRepository`, and `IUnitOfWork`.

**Method signatures:**

```
GetBySectionAndYearAsync(Guid sectionId, Guid academicYearId, CancellationToken) → List<EnrollmentDto>
CreateAsync(Guid sectionId, CreateEnrollmentRequest, CancellationToken) → EnrollmentDto
TransferAsync(Guid enrollmentId, TransferEnrollmentRequest, CancellationToken) → EnrollmentDto
DeleteAsync(Guid enrollmentId, CancellationToken) → void
```

**Algorithms:**

```
GetBySectionAndYearAsync(Guid sectionId, Guid academicYearId, CancellationToken) → List<EnrollmentDto>
  await gradeRepository.GetSectionByIdAsync(sectionId, ct)
      ?? throw NotFoundException("Section not found.")
  enrollments = await enrollmentRepository.GetBySectionAndYearAsync(sectionId, academicYearId, ct)
  return enrollments.Select(ToDto).ToList()

CreateAsync(Guid sectionId, CreateEnrollmentRequest, CancellationToken) → EnrollmentDto
  await gradeRepository.GetSectionByIdAsync(sectionId, ct)
      ?? throw NotFoundException("Section not found.")
  await studentRepository.GetByIdAsync(request.StudentId, ct)
      ?? throw NotFoundException("Student not found.")
  await academicYearRepository.GetByIdAsync(request.AcademicYearId, ct)
      ?? throw NotFoundException("Academic year not found.")
  existing = await enrollmentRepository.GetByStudentAndYearAsync(request.StudentId, request.AcademicYearId, ct)
  if existing != null:
      throw ConflictException("Student is already enrolled for this academic year.")
  enrollment = new StudentSectionEnrollment
      { StudentId = request.StudentId, SectionId = sectionId, AcademicYearId = request.AcademicYearId }
  await enrollmentRepository.AddAsync(enrollment, ct)
  await unitOfWork.SaveChangesAsync(ct)
  loaded = await enrollmentRepository.GetByIdWithDetailsAsync(enrollment.Id, ct)!
  return ToDto(loaded)

TransferAsync(Guid enrollmentId, TransferEnrollmentRequest, CancellationToken) → EnrollmentDto
  enrollment = await enrollmentRepository.GetByIdWithDetailsAsync(enrollmentId, ct)
      ?? throw NotFoundException("Enrollment not found.")
  await gradeRepository.GetSectionByIdAsync(request.SectionId, ct)
      ?? throw NotFoundException("Section not found.")
  // Tenant scoping (global query filter) already guarantees cross-school
  // assignments are invisible; no additional school check needed.
  enrollment.SectionId = request.SectionId
  enrollmentRepository.Update(enrollment)
  await unitOfWork.SaveChangesAsync(ct)
  loaded = await enrollmentRepository.GetByIdWithDetailsAsync(enrollment.Id, ct)!
  return ToDto(loaded)

DeleteAsync(Guid enrollmentId, CancellationToken) → void
  enrollment = await enrollmentRepository.GetByIdAsync(enrollmentId, ct)
      ?? throw NotFoundException("Enrollment not found.")
  enrollmentRepository.Remove(enrollment)
  await unitOfWork.SaveChangesAsync(ct)
```

#### `TeacherAssignmentService`

Injects `ITeacherSectionSubjectRepository`, `ITeacherRepository`, `ISubjectRepository`, `IGradeRepository`, `IAcademicYearRepository`, and `IUnitOfWork`.

**Method signatures:**

```
GetByTeacherAndYearAsync(Guid teacherId, Guid academicYearId, CancellationToken) → List<TeacherAssignmentDto>
CreateAsync(Guid teacherId, CreateTeacherAssignmentRequest, CancellationToken) → TeacherAssignmentDto
DeleteAsync(Guid teacherId, Guid assignmentId, CancellationToken) → void
```

**Algorithms:**

```
GetByTeacherAndYearAsync(Guid teacherId, Guid academicYearId, CancellationToken) → List<TeacherAssignmentDto>
  await teacherRepository.GetByIdAsync(teacherId, ct)
      ?? throw NotFoundException("Teacher not found.")
  assignments = await assignmentRepository.GetByTeacherAndYearAsync(teacherId, academicYearId, ct)
  return assignments.Select(ToDto).ToList()

CreateAsync(Guid teacherId, CreateTeacherAssignmentRequest, CancellationToken) → TeacherAssignmentDto
  await teacherRepository.GetByIdAsync(teacherId, ct)
      ?? throw NotFoundException("Teacher not found.")
  await subjectRepository.GetByIdAsync(request.SubjectId, ct)
      ?? throw NotFoundException("Subject not found.")
  await gradeRepository.GetSectionByIdAsync(request.SectionId, ct)
      ?? throw NotFoundException("Section not found.")
  await academicYearRepository.GetByIdAsync(request.AcademicYearId, ct)
      ?? throw NotFoundException("Academic year not found.")
  existing = await assignmentRepository.GetBySubjectSectionAndYearAsync(
      request.SubjectId, request.SectionId, request.AcademicYearId, ct)
  if existing != null:
      throw ConflictException(
          "A teacher is already assigned to this subject in this section for this academic year.")
  assignment = new TeacherSectionSubject
      { TeacherId = teacherId, SubjectId = request.SubjectId,
        SectionId = request.SectionId, AcademicYearId = request.AcademicYearId }
  await assignmentRepository.AddAsync(assignment, ct)
  await unitOfWork.SaveChangesAsync(ct)
  // Reload with navigation properties for the response
  loaded = await assignmentRepository.GetByTeacherAndYearAsync(teacherId, request.AcademicYearId, ct)
  return ToDto(loaded.Single(a => a.Id == assignment.Id))

DeleteAsync(Guid teacherId, Guid assignmentId, CancellationToken) → void
  assignment = await assignmentRepository.GetByIdAsync(assignmentId, ct)
      ?? throw NotFoundException("Teacher assignment not found.")
  if assignment.TeacherId != teacherId:
      throw NotFoundException("Teacher assignment not found.")
  assignmentRepository.Remove(assignment)
  await unitOfWork.SaveChangesAsync(ct)
```

#### `DependencyInjection.cs` (Application)

Append to `AddApplication`:

```csharp
services.AddScoped<EnrollmentService>();
services.AddScoped<TeacherAssignmentService>();
```

---

### Infrastructure — `SchoolMgmt.Infrastructure`

#### Modified: `GradeRepository`

Add implementation for `GetSectionByIdAsync`:

```csharp
public async Task<Section?> GetSectionByIdAsync(Guid sectionId, CancellationToken ct = default)
{
    return await Context.Set<Section>()
        .Include(s => s.Grade)
        .FirstOrDefaultAsync(s => s.Id == sectionId, ct);
}
```

The EF Core global query filter on `SchoolId` applied to `Section` ensures this only returns sections belonging to the current tenant.

#### `StudentSectionEnrollmentConfiguration`

```csharp
// Table: StudentSectionEnrollments
builder.ToTable("StudentSectionEnrollments");
builder.HasKey(x => x.Id);
builder.HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();

builder.HasOne(x => x.Student)
    .WithMany()
    .HasForeignKey(x => x.StudentId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.Section)
    .WithMany()
    .HasForeignKey(x => x.SectionId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.AcademicYear)
    .WithMany()
    .HasForeignKey(x => x.AcademicYearId)
    .OnDelete(DeleteBehavior.Restrict);
```

`RESTRICT` on all FKs: a student, section, or academic year with active enrollments cannot be deleted. No cascade or set-null behaviors — assignment records must be explicitly removed first.

#### `TeacherSectionSubjectConfiguration`

```csharp
// Table: TeacherSectionSubjects
builder.ToTable("TeacherSectionSubjects");
builder.HasKey(x => x.Id);
builder.HasIndex(x => new { x.SubjectId, x.SectionId, x.AcademicYearId }).IsUnique();

builder.HasOne(x => x.Teacher)
    .WithMany()
    .HasForeignKey(x => x.TeacherId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.Subject)
    .WithMany()
    .HasForeignKey(x => x.SubjectId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.Section)
    .WithMany()
    .HasForeignKey(x => x.SectionId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.AcademicYear)
    .WithMany()
    .HasForeignKey(x => x.AcademicYearId)
    .OnDelete(DeleteBehavior.Restrict);
```

#### `StudentSectionEnrollmentRepository`

```csharp
internal sealed class StudentSectionEnrollmentRepository(AppDbContext context)
    : Repository<StudentSectionEnrollment>(context), IStudentSectionEnrollmentRepository
{
    public async Task<List<StudentSectionEnrollment>> GetBySectionAndYearAsync(
        Guid sectionId, Guid academicYearId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(e => e.Student)
            .Include(e => e.Section).ThenInclude(s => s.Grade)
            .Include(e => e.AcademicYear)
            .Where(e => e.SectionId == sectionId && e.AcademicYearId == academicYearId)
            .OrderBy(e => e.Student.LastName)
            .ThenBy(e => e.Student.FirstName)
            .ToListAsync(ct);
    }

    public async Task<StudentSectionEnrollment?> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.AcademicYearId == academicYearId, ct);
    }

    public async Task<StudentSectionEnrollment?> GetByIdWithDetailsAsync(
        Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(e => e.Student)
            .Include(e => e.Section).ThenInclude(s => s.Grade)
            .Include(e => e.AcademicYear)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }
}
```

#### `TeacherSectionSubjectRepository`

```csharp
internal sealed class TeacherSectionSubjectRepository(AppDbContext context)
    : Repository<TeacherSectionSubject>(context), ITeacherSectionSubjectRepository
{
    public async Task<List<TeacherSectionSubject>> GetByTeacherAndYearAsync(
        Guid teacherId, Guid academicYearId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(a => a.Subject)
            .Include(a => a.Section).ThenInclude(s => s.Grade)
            .Include(a => a.AcademicYear)
            .Where(a => a.TeacherId == teacherId && a.AcademicYearId == academicYearId)
            .OrderBy(a => a.Section.Grade.DisplayOrder)
            .ThenBy(a => a.Section.Name)
            .ThenBy(a => a.Subject.Name)
            .ToListAsync(ct);
    }

    public async Task<TeacherSectionSubject?> GetBySubjectSectionAndYearAsync(
        Guid subjectId, Guid sectionId, Guid academicYearId, CancellationToken ct = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(
                a => a.SubjectId == subjectId
                  && a.SectionId == sectionId
                  && a.AcademicYearId == academicYearId,
                ct);
    }
}
```

#### Modified: `AppDbContext`

Add two `DbSet` properties:

```csharp
public DbSet<StudentSectionEnrollment> StudentSectionEnrollments => Set<StudentSectionEnrollment>();
public DbSet<TeacherSectionSubject>    TeacherSectionSubjects    => Set<TeacherSectionSubject>();
```

#### `DependencyInjection.cs` (Infrastructure)

Add to `AddInfrastructure`:

```csharp
services.AddScoped<IStudentSectionEnrollmentRepository, StudentSectionEnrollmentRepository>();
services.AddScoped<ITeacherSectionSubjectRepository,    TeacherSectionSubjectRepository>();
```

---

### WebApi — `SchoolMgmt.WebApi`

#### `SectionEnrollmentsController`

Route prefix: `/api/sections/{sectionId}/enrollments`. All endpoints `[Authorize(Roles = "Admin")]`.

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `GET` | `/api/sections/{sectionId}/enrollments` | `GetBySectionAndYearAsync` | 200 — `List<EnrollmentDto>` | `academicYearId` query param required; 400 if missing; 404 if section not found |
| `POST` | `/api/sections/{sectionId}/enrollments` | `CreateAsync` | 201 + `Location: /api/enrollments/{id}` | 404 if section/student/year not found; 409 if student already enrolled for that year |

#### `EnrollmentsController`

Route prefix: `/api/enrollments`. All endpoints `[Authorize(Roles = "Admin")]`.

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `PUT` | `/api/enrollments/{id}` | `TransferAsync` | 200 — `EnrollmentDto` | 404 if enrollment or new section not found |
| `DELETE` | `/api/enrollments/{id}` | `DeleteAsync` | 204 No Content | 404 if not found |

#### `TeacherAssignmentsController`

Route prefix: `/api/teachers/{teacherId}/assignments`. All endpoints `[Authorize(Roles = "Admin")]`.

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `GET` | `/api/teachers/{teacherId}/assignments` | `GetByTeacherAndYearAsync` | 200 — `List<TeacherAssignmentDto>` | `academicYearId` query param required; 400 if missing; 404 if teacher not found |
| `POST` | `/api/teachers/{teacherId}/assignments` | `CreateAsync` | 201 + `Location: /api/teachers/{teacherId}/assignments/{id}` | 404 if teacher/subject/section/year not found; 409 if subject-section-year slot already taken |
| `DELETE` | `/api/teachers/{teacherId}/assignments/{id}` | `DeleteAsync` | 204 No Content | 404 if not found or doesn't belong to this teacher |

`academicYearId` as a required query parameter: return 400 if absent (use `[Required]` on the parameter or manual check).

---

## Project Structure

New and modified files introduced by this spec:

```
backend/
  SchoolMgmt.Domain/
    Entities/
      StudentSectionEnrollment.cs                     # new
      TeacherSectionSubject.cs                        # new

  SchoolMgmt.Application/
    Enrollments/
      IStudentSectionEnrollmentRepository.cs          # new
      EnrollmentService.cs                            # new
      Dtos/
        EnrollmentDto.cs                              # new
        CreateEnrollmentRequest.cs                    # new
        TransferEnrollmentRequest.cs                  # new
      Validators/
        CreateEnrollmentRequestValidator.cs           # new
        TransferEnrollmentRequestValidator.cs         # new
    TeacherAssignments/
      ITeacherSectionSubjectRepository.cs             # new
      TeacherAssignmentService.cs                     # new
      Dtos/
        TeacherAssignmentDto.cs                       # new
        CreateTeacherAssignmentRequest.cs             # new
      Validators/
        CreateTeacherAssignmentRequestValidator.cs    # new
    Grades/
      IGradeRepository.cs                             # add GetSectionByIdAsync
    DependencyInjection.cs                            # add both services

  SchoolMgmt.Infrastructure/
    Persistence/
      AppDbContext.cs                                 # add 2 DbSets
      Configurations/
        StudentSectionEnrollmentConfiguration.cs      # new
        TeacherSectionSubjectConfiguration.cs         # new
      Repositories/
        GradeRepository.cs                            # add GetSectionByIdAsync
        StudentSectionEnrollmentRepository.cs         # new
        TeacherSectionSubjectRepository.cs            # new
      Migrations/
        <AddClassSectionAssignment>                   # new — EF Core generated
    DependencyInjection.cs                            # add both repository registrations

  SchoolMgmt.WebApi/
    Controllers/
      SectionEnrollmentsController.cs                 # new
      EnrollmentsController.cs                        # new
      TeacherAssignmentsController.cs                 # new

tests/
  SchoolMgmt.IntegrationTests/
    EnrollmentsControllerTests.cs                     # new — real Postgres via Testcontainers
    TeacherAssignmentsControllerTests.cs              # new — real Postgres via Testcontainers
```

---

## Commands

```
Build:          dotnet build SchoolMgmt.slnx
Test:           dotnet test SchoolMgmt.slnx
Add migration:  dotnet ef migrations add AddClassSectionAssignment --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
Apply locally:  dotnet ef database update --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

---

## Database Schema

### New: `StudentSectionEnrollments`

| Column | PG Type | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|
| `Id` | `uuid` | NOT NULL | PK | — | Surrogate primary key |
| `SchoolId` | `uuid` | NOT NULL | — | — | Tenant scope (auto-stamped) |
| `StudentId` | `uuid` | NOT NULL | FK → `Students.Id` | ON DELETE RESTRICT | The enrolled student |
| `SectionId` | `uuid` | NOT NULL | FK → `Sections.Id` | ON DELETE RESTRICT | The assigned section; updatable (transfer) |
| `AcademicYearId` | `uuid` | NOT NULL | FK → `AcademicYears.Id` | ON DELETE RESTRICT | The academic year of placement |
| `CreatedAt` | `timestamptz` | NOT NULL | — | — | Auto-stamped |
| `UpdatedAt` | `timestamptz` | NULL | — | — | Auto-stamped on modification |

Unique index: `(StudentId, AcademicYearId)` — one placement per student per year.

### New: `TeacherSectionSubjects`

| Column | PG Type | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|
| `Id` | `uuid` | NOT NULL | PK | — | Surrogate primary key |
| `SchoolId` | `uuid` | NOT NULL | — | — | Tenant scope (auto-stamped) |
| `TeacherId` | `uuid` | NOT NULL | FK → `Teachers.Id` | ON DELETE RESTRICT | The assigned teacher |
| `SubjectId` | `uuid` | NOT NULL | FK → `Subjects.Id` | ON DELETE RESTRICT | The subject being taught |
| `SectionId` | `uuid` | NOT NULL | FK → `Sections.Id` | ON DELETE RESTRICT | The section in which the subject is taught |
| `AcademicYearId` | `uuid` | NOT NULL | FK → `AcademicYears.Id` | ON DELETE RESTRICT | The academic year of the assignment |
| `CreatedAt` | `timestamptz` | NOT NULL | — | — | Auto-stamped |
| `UpdatedAt` | `timestamptz` | NULL | — | — | Auto-stamped on modification |

Unique index: `(SubjectId, SectionId, AcademicYearId)` — one teacher per subject per section per year.

---

## Testing Strategy

No domain unit tests — neither entity has domain behavior (no guards, no invariants, no factory methods).

### Integration tests — `EnrollmentsControllerTests.cs`

Against real Postgres (Testcontainers), authenticated as the demo Admin. Seed data: one `AcademicYear`, one `Grade` with two `Section`s (`5-A`, `5-B`), and two `Student` rows.

**Enroll student:**
- `POST /api/sections/{5A-id}/enrollments` with valid StudentId + AcademicYearId → 201; response has student name, section name, grade name
- `POST /api/sections/{5A-id}/enrollments` with unknown StudentId → 404
- `POST /api/sections/{5A-id}/enrollments` with unknown AcademicYearId → 404
- `POST /api/sections/{unknown-id}/enrollments` → 404 (section not found)
- `POST /api/sections/{5A-id}/enrollments` — enroll same student twice in same year → 409
- `POST /api/sections/{5A-id}/enrollments` with missing StudentId → 400

**List enrollments:**
- `GET /api/sections/{5A-id}/enrollments?academicYearId={yearId}` after enrolling two students → 200, returns both in last-name order
- `GET /api/sections/{5A-id}/enrollments` (missing academicYearId) → 400
- `GET /api/sections/{5A-id}/enrollments?academicYearId={yearId}` with no enrollments → 200, empty list
- `GET /api/sections/{unknown-id}/enrollments?academicYearId={yearId}` → 404

**Transfer student:**
- `PUT /api/enrollments/{id}` with SectionId = 5-B → 200; response shows new section name
- `PUT /api/enrollments/{id}` with unknown SectionId → 404
- `PUT /api/enrollments/{unknown-id}` → 404
- After transfer: `GET /api/sections/{5A-id}/enrollments` no longer includes the student; `GET /api/sections/{5B-id}/enrollments` does

**Grade change (transfer to different grade's section):**
- Seed a second grade (`Grade 6`) with section `6-A`
- `PUT /api/enrollments/{id}` with SectionId = 6-A → 200; GradeId in response is Grade 6's Id (grade changed implicitly)

**Delete enrollment:**
- `DELETE /api/enrollments/{id}` → 204
- Subsequent `GET /api/sections/{sectionId}/enrollments?academicYearId=...` no longer includes the student
- `DELETE /api/enrollments/{unknown-id}` → 404
- After delete: same student can be re-enrolled → 201 (no phantom unique constraint)

**Auth:**
- Unauthenticated requests → 401
- Teacher-role requests → 403

### Integration tests — `TeacherAssignmentsControllerTests.cs`

Seed data: one `AcademicYear`, one `Grade` with two `Section`s, two `Teacher` rows, two `Subject` rows.

**Assign teacher:**
- `POST /api/teachers/{teacherId}/assignments` with valid SubjectId + SectionId + AcademicYearId → 201; response has subject name, section name, grade name
- `POST /api/teachers/{teacherId}/assignments` with unknown SubjectId → 404
- `POST /api/teachers/{teacherId}/assignments` with unknown SectionId → 404
- `POST /api/teachers/{teacherId}/assignments` with unknown AcademicYearId → 404
- `POST /api/teachers/{unknown-id}/assignments` → 404
- `POST /api/teachers/{teacherId}/assignments` — assign same teacher to same subject+section+year twice → 409
- `POST /api/teachers/{teacher2Id}/assignments` with same SubjectId+SectionId+AcademicYearId as teacher1 → 409 (slot already taken by teacher1)
- `POST /api/teachers/{teacherId}/assignments` — same teacher, same subject, different section → 201 (allowed)
- `POST /api/teachers/{teacherId}/assignments` with missing SubjectId → 400

**List assignments:**
- `GET /api/teachers/{teacherId}/assignments?academicYearId={yearId}` after two assignments → 200, ordered by grade/section/subject
- `GET /api/teachers/{teacherId}/assignments` (missing academicYearId) → 400
- `GET /api/teachers/{unknown-id}/assignments?academicYearId={yearId}` → 404

**Delete assignment:**
- `DELETE /api/teachers/{teacherId}/assignments/{id}` → 204
- Subsequent `GET` no longer includes the assignment
- `DELETE /api/teachers/{teacherId}/assignments/{unknown-id}` → 404
- `DELETE /api/teachers/{teacher1Id}/assignments/{teacher2AssignmentId}` → 404 (cross-teacher delete blocked)
- After delete: same slot can be reassigned to same or different teacher → 201

**Auth:**
- Unauthenticated requests → 401
- Teacher-role requests → 403

---

## Boundaries

- **Always:** call `dotnet test SchoolMgmt.slnx` before considering any task done; follow all rules in `.claude/rules/backend.md`; keep GET endpoints side-effect-free; use RESTRICT (not CASCADE) on all FKs — assignment records must be explicitly removed.
- **Ask first:** changing `DELETE /api/enrollments/{id}` to a soft-delete / "withdrawn" status (currently hard delete per idea doc); adding a homeroom/class teacher designation; exposing a bulk-enrollment endpoint.
- **Never:** call `SaveChangesAsync` or transaction methods from inside a repository; call a repository from a controller directly; allow enrollment without an explicit `AcademicYearId` — year-scoping is mandatory.

---

## Success Criteria

- `POST /api/sections/{sectionId}/enrollments` returns 201 with `EnrollmentDto` containing student code, section name, and grade name — integration test passes.
- Enrolling the same student twice for the same academic year returns 409 — integration test confirms.
- `PUT /api/enrollments/{id}` with a new SectionId from a different grade updates `GradeId` in the response implicitly (via Section → Grade navigation) — integration test confirms grade-change case.
- `DELETE /api/enrollments/{id}` followed by re-enrollment succeeds with 201 — integration test confirms no phantom unique-constraint violation.
- `POST /api/teachers/{teacherId}/assignments` with the same SubjectId+SectionId+AcademicYearId for a different teacher returns 409 — integration test confirms one-teacher-per-slot rule.
- `DELETE /api/teachers/{teacher1Id}/assignments/{teacher2AssignmentId}` returns 404 — integration test confirms cross-teacher delete is blocked.
- All FK columns carry `RESTRICT` delete behavior — confirmed via EF Core migration output.
- All endpoints return 401 for unauthenticated requests and 403 for Teacher-role requests — integration tests confirm.
- `dotnet test SchoolMgmt.slnx` passes with all new and existing tests green.

---

## Open Questions

- **Inactive student enrollment guard:** should `CreateAsync` warn (or block) if the student's `EnrollmentStatus` is not `Active`? The idea doc suggests a warning (not a block). Defer to implementation review — a simple 400 with a clear message on `Withdrawn`/`Graduated` students is the likely call.
- **Inactive teacher assignment guard:** should assigning a teacher with `IsActive = false` be blocked? Likely yes — but confirm during implementation.
- **`academicYearId` as required query param:** returning 400 is the current spec. If the frontend ever needs "all years," this is easy to relax later by making the param optional (returns all when absent).
