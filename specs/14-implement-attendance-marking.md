# Spec 14 — Implement Attendance Marking (Backend + Frontend)

## Related Docs & Prior Specs

- **Idea doc**: [docs/ideas/11-attendance-marking.md](../docs/ideas/11-attendance-marking.md)
- **Student section enrollment** (roster source): [specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md)
- **Teacher assignments** (authorization anchor): [specs/11-implement-class-section-assignment-frontend.md](11-implement-class-section-assignment-frontend.md)
- **Multi-tenant base**: [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md)

## Overview

Teachers mark a daily roll-call status (Present / Late / Absent / Excused) for each student in their section. One status per student per section per date — bulk-submitted as a whole day at once. Admin gets a read-only view. Records feed the parent portal and overview dashboard later.

---

## Part A — Backend

### A1. New Enum: `AttendanceStatus`

```csharp
// Domain/Enums/AttendanceStatus.cs
public enum AttendanceStatus
{
    Present = 0,
    Late    = 1,
    Absent  = 2,
    Excused = 3,
}
```

### A2. New Domain Entity: `AttendanceRecord`

```csharp
// Domain/Entities/AttendanceRecord.cs
public class AttendanceRecord : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }

    public Guid MarkedByUserId { get; set; }
    public User MarkedByUser { get; set; } = null!;
}
```

**Unique constraint**: `(SchoolId, StudentId, SectionId, Date)` — one status per student per section per day.

### A3. EF Core Configuration

```csharp
// Infrastructure/Persistence/Configurations/AttendanceRecordConfiguration.cs
public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");

        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.Status).HasConversion<string>();

        builder.HasIndex(e => new { e.SchoolId, e.StudentId, e.SectionId, e.Date }).IsUnique();

        builder.HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Section)
            .WithMany()
            .HasForeignKey(e => e.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AcademicYear)
            .WithMany()
            .HasForeignKey(e => e.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.MarkedByUser)
            .WithMany()
            .HasForeignKey(e => e.MarkedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### A4. AppDbContext Addition

```csharp
public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
```

Register the configuration in `OnModelCreating`:
```csharp
modelBuilder.ApplyConfiguration(new AttendanceRecordConfiguration());
```

### A5. Repository Interface

```csharp
// Application/Attendance/IAttendanceRepository.cs
public interface IAttendanceRepository : IRepository<AttendanceRecord>
{
    Task<List<AttendanceRecord>> GetBySectionAndDateAsync(
        Guid sectionId, DateOnly date, CancellationToken ct = default);

    Task<AttendanceRecord?> GetByStudentSectionAndDateAsync(
        Guid studentId, Guid sectionId, DateOnly date, CancellationToken ct = default);

    Task<List<AttendanceRecord>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);
}
```

### A6. Repository Implementation

```csharp
// Infrastructure/Persistence/Repositories/AttendanceRepository.cs
internal sealed class AttendanceRepository(AppDbContext context)
    : Repository<AttendanceRecord>(context), IAttendanceRepository
{
    public Task<List<AttendanceRecord>> GetBySectionAndDateAsync(
        Guid sectionId, DateOnly date, CancellationToken ct = default) =>
        DbSet
            .Include(r => r.Student)
            .Where(r => r.SectionId == sectionId && r.Date == date)
            .ToListAsync(ct);

    public Task<AttendanceRecord?> GetByStudentSectionAndDateAsync(
        Guid studentId, Guid sectionId, DateOnly date, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(
            r => r.StudentId == studentId && r.SectionId == sectionId && r.Date == date, ct);

    public Task<List<AttendanceRecord>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .Include(r => r.Section)
            .Where(r => r.StudentId == studentId && r.AcademicYearId == academicYearId)
            .OrderBy(r => r.Date)
            .ToListAsync(ct);
}
```

### A7. ITeacherRepository Addition

Add to `ITeacherRepository`:
```csharp
Task<Teacher?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
```

Implement in `TeacherRepository`:
```csharp
public Task<Teacher?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
    DbSet.FirstOrDefaultAsync(t => t.UserId == userId, ct);
```

### A8. DTOs

```csharp
// Application/Attendance/Dtos/

// Roster entry — one per enrolled student for a section+date
public record AttendanceRosterEntryDto(
    Guid StudentId,
    string StudentName,
    string StudentCode,
    string? Status,   // null if not yet marked for this date
    string? Notes
);

// Roster response for GET section-roster
public record SectionAttendanceRosterDto(
    Guid SectionId,
    string SectionName,
    DateOnly Date,
    List<AttendanceRosterEntryDto> Entries
);

// Bulk upsert request body
public record BulkUpsertAttendanceRequest(
    Guid SectionId,
    Guid AcademicYearId,
    DateOnly Date,
    List<AttendanceEntryRequest> Entries
);

public record AttendanceEntryRequest(
    Guid StudentId,
    string Status,    // "Present" | "Late" | "Absent" | "Excused"
    string? Notes
);

// Bulk upsert response
public record BulkUpsertAttendanceResult(int Upserted);

// Student history entry — for GET student-history
public record AttendanceHistoryEntryDto(
    Guid Id,
    Guid SectionId,
    string SectionName,
    DateOnly Date,
    string Status,
    string? Notes
);
```

### A9. Validator

```csharp
// Application/Attendance/Validators/BulkUpsertAttendanceRequestValidator.cs
public class BulkUpsertAttendanceRequestValidator : AbstractValidator<BulkUpsertAttendanceRequest>
{
    private static readonly string[] ValidStatuses = ["Present", "Late", "Absent", "Excused"];

    public BulkUpsertAttendanceRequestValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Entries).NotEmpty().WithMessage("At least one entry is required.");
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.StudentId).NotEmpty();
            entry.RuleFor(e => e.Status)
                .Must(s => ValidStatuses.Contains(s))
                .WithMessage("Status must be Present, Late, Absent, or Excused.");
            entry.RuleFor(e => e.Notes).MaximumLength(500);
        });
    }
}
```

### A10. AttendanceService

```csharp
// Application/Attendance/AttendanceService.cs
public class AttendanceService(
    IAttendanceRepository attendanceRepo,
    IStudentSectionEnrollmentRepository enrollmentRepo,
    ITeacherSectionSubjectRepository assignmentRepo,
    ITeacherRepository teacherRepo,
    IRepository<Section> sectionRepo,
    IUnitOfWork unitOfWork)
{
    // GET section roster — returns enrolled students with current status for the date
    // Called by both Teacher and Admin
    public async Task<SectionAttendanceRosterDto> GetSectionRosterAsync(
        Guid sectionId, DateOnly date, Guid academicYearId, CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(sectionId, ct)
            ?? throw new NotFoundException("Section not found.");

        var enrollments = await enrollmentRepo.GetBySectionAndYearAsync(sectionId, academicYearId, ct);
        var existing = await attendanceRepo.GetBySectionAndDateAsync(sectionId, date, ct);
        var existingByStudent = existing.ToDictionary(r => r.StudentId);

        var entries = enrollments.Select(e => new AttendanceRosterEntryDto(
            e.StudentId,
            $"{e.Student.FirstName} {e.Student.LastName}",
            e.Student.StudentCode,
            existingByStudent.TryGetValue(e.StudentId, out var rec) ? rec.Status.ToString() : null,
            existingByStudent.TryGetValue(e.StudentId, out var rec2) ? rec2.Notes : null
        )).ToList();

        return new SectionAttendanceRosterDto(sectionId, section.Name, date, entries);
    }

    // PUT bulk — upsert attendance for a section+date
    // Teacher only; validates teacher is assigned to the section
    public async Task<BulkUpsertAttendanceResult> BulkUpsertAsync(
        BulkUpsertAttendanceRequest request, Guid markedByUserId, CancellationToken ct = default)
    {
        // Validate section exists
        _ = await sectionRepo.GetByIdAsync(request.SectionId, ct)
            ?? throw new NotFoundException("Section not found.");

        // Validate the calling user is a teacher assigned to this section
        var teacher = await teacherRepo.GetByUserIdAsync(markedByUserId, ct)
            ?? throw new NotFoundException("Teacher profile not found.");
        var assignments = await assignmentRepo.GetByTeacherAndYearAsync(teacher.Id, request.AcademicYearId, ct);
        if (!assignments.Any(a => a.SectionId == request.SectionId))
            throw new DomainException("You are not assigned to this section for the selected year.");

        var upserted = 0;
        foreach (var entry in request.Entries)
        {
            if (!Enum.TryParse<AttendanceStatus>(entry.Status, out var status))
                continue;

            var existing = await attendanceRepo.GetByStudentSectionAndDateAsync(
                entry.StudentId, request.SectionId, request.Date, ct);

            if (existing is not null)
            {
                existing.Status = status;
                existing.Notes = entry.Notes;
                existing.MarkedByUserId = markedByUserId;
                attendanceRepo.Update(existing);
            }
            else
            {
                var record = new AttendanceRecord
                {
                    StudentId = entry.StudentId,
                    SectionId = request.SectionId,
                    AcademicYearId = request.AcademicYearId,
                    Date = request.Date,
                    Status = status,
                    Notes = entry.Notes,
                    MarkedByUserId = markedByUserId,
                };
                await attendanceRepo.AddAsync(record, ct);
            }
            upserted++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new BulkUpsertAttendanceResult(upserted);
    }

    // GET student history — full attendance log for a student for a year
    // Teacher and Admin
    public async Task<List<AttendanceHistoryEntryDto>> GetStudentHistoryAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var records = await attendanceRepo.GetByStudentAndYearAsync(studentId, academicYearId, ct);
        return records.Select(r => new AttendanceHistoryEntryDto(
            r.Id,
            r.SectionId,
            r.Section.Name,
            r.Date,
            r.Status.ToString(),
            r.Notes
        )).ToList();
    }
}
```

### A11. AttendanceController

```csharp
// WebApi/Controllers/AttendanceController.cs
[ApiController]
[Route("api/attendance")]
[Authorize(Roles = "Admin,Teacher")]
public class AttendanceController(AttendanceService service) : ControllerBase
{
    // GET /api/attendance/section-roster?sectionId=&date=&academicYearId=
    // Read-only; Admin and Teacher both allowed
    [HttpGet("section-roster")]
    public async Task<IActionResult> GetSectionRoster(
        [FromQuery] Guid sectionId,
        [FromQuery] DateOnly date,
        [FromQuery] Guid academicYearId,
        CancellationToken ct)
    {
        var result = await service.GetSectionRosterAsync(sectionId, date, academicYearId, ct);
        return Ok(result);
    }

    // PUT /api/attendance/bulk
    // Teacher only; ownership validated in service
    [HttpPut("bulk")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> BulkUpsert(
        [FromBody] BulkUpsertAttendanceRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await service.BulkUpsertAsync(request, userId, ct);
        return Ok(result);
    }

    // GET /api/attendance/student-history?studentId=&academicYearId=
    [HttpGet("student-history")]
    public async Task<IActionResult> GetStudentHistory(
        [FromQuery] Guid studentId,
        [FromQuery] Guid academicYearId,
        CancellationToken ct)
    {
        var result = await service.GetStudentHistoryAsync(studentId, academicYearId, ct);
        return Ok(result);
    }
}
```

Import needed: `using System.Security.Claims;`

### A12. DI Registration

In `Infrastructure/DependencyInjection.cs`, register:
```csharp
services.AddScoped<IAttendanceRepository, AttendanceRepository>();
```

In `Application/DependencyInjection.cs`, register:
```csharp
services.AddScoped<AttendanceService>();
```

### A13. Migration

```bash
dotnet ef migrations add AddAttendanceRecords \
  --project SchoolMgmt.Infrastructure \
  --startup-project SchoolMgmt.WebApi
```

---

## Part B — Frontend

### B1. API Client

```ts
// frontend/src/api/attendance.ts

export type AttendanceStatus = 'Present' | 'Late' | 'Absent' | 'Excused'
export const ATTENDANCE_STATUSES: AttendanceStatus[] = ['Present', 'Late', 'Absent', 'Excused']

export interface AttendanceRosterEntry {
  studentId: string
  studentName: string
  studentCode: string
  status: AttendanceStatus | null
  notes: string | null
}

export interface SectionAttendanceRoster {
  sectionId: string
  sectionName: string
  date: string          // "YYYY-MM-DD"
  entries: AttendanceRosterEntry[]
}

export interface AttendanceEntryRequest {
  studentId: string
  status: AttendanceStatus
  notes?: string | null
}

export interface BulkUpsertAttendanceRequest {
  sectionId: string
  academicYearId: string
  date: string          // "YYYY-MM-DD"
  entries: AttendanceEntryRequest[]
}

export interface AttendanceHistoryEntry {
  id: string
  sectionId: string
  sectionName: string
  date: string
  status: AttendanceStatus
  notes: string | null
}

export const ATTENDANCE_KEYS = {
  sectionRoster: (sectionId: string, date: string, academicYearId: string) =>
    ['attendance', 'section-roster', sectionId, date, academicYearId] as const,
  studentHistory: (studentId: string, academicYearId: string) =>
    ['attendance', 'student-history', studentId, academicYearId] as const,
}

export const attendanceApi = {
  getSectionRoster: (sectionId: string, date: string, academicYearId: string) =>
    api.get<SectionAttendanceRoster>('/attendance/section-roster', {
      params: { sectionId, date, academicYearId },
    }).then(r => r.data),

  bulkUpsert: (request: BulkUpsertAttendanceRequest) =>
    api.put<{ upserted: number }>('/attendance/bulk', request).then(r => r.data),

  getStudentHistory: (studentId: string, academicYearId: string) =>
    api.get<AttendanceHistoryEntry[]>('/attendance/student-history', {
      params: { studentId, academicYearId },
    }).then(r => r.data),
}
```

Import `api` from `'./client'` (the existing Axios instance).

### B2. Teacher Attendance Page

**Route**: `/teacher/attendance`
**Role**: Teacher only

```
frontend/src/pages/teacher/
  attendance/
    AttendancePage.tsx      — section picker + date + roster form
```

**Page structure**:

1. **Section + Year picker**: On mount, fetch the teacher's section assignments for the active academic year (reuse the existing teacher assignments query, scoped to the logged-in teacher). Show a `Select` of distinct sections. If the teacher has only one section, auto-select it.

2. **Date picker**: `<Input type="date" />` defaulting to today (`new Date().toISOString().slice(0, 10)`).

3. **Roster table** (loaded when both section and date are selected):
   - Columns: Student name + code, Status (Select dropdown), Notes (text input)
   - Status options: Present / Late / Absent / Excused
   - Pre-populated if records already exist for the selected section+date
   - "Mark All Present" convenience button — sets all unset entries to Present
   - **Submit** button → `PUT /api/attendance/bulk` → success toast

4. **Unsaved changes guard**: If the form is dirty (any status changed), warn before navigating away (use `useBlocker` as in `FeeTemplatePage`).

**Status badge colors** (for display in read-only views):
- Present → green
- Late → yellow/amber
- Absent → red
- Excused → blue

### B3. Teacher Routes + App Shell

Add a **Teacher** section to the app shell nav, visible only to users with Role = "Teacher":
```ts
{ label: 'Attendance', to: '/teacher/attendance', icon: <ClipboardList size={18} />, roles: ['Teacher'] }
```

Add routes:
```tsx
// In the existing admin routes file or a new teacher routes file
<Route path="teacher/attendance" element={<AttendancePage />} />
```

Route guard: `RequireRole("Teacher")` (or the existing role-guard pattern — check `AppShell.tsx` and the route guard implementation in the codebase).

### B4. Admin Read-Only Attendance View

**Route**: `/admin/attendance`

Admin page: Grade → Section → Date → read-only roster table.

```
frontend/src/pages/admin/
  attendance/
    AttendanceViewPage.tsx
```

**Page structure**:
1. Grade `Select` → filters section list
2. Section `Select` (filtered by grade)
3. Date picker
4. Read-only table: student name, student code, status badge, notes
5. If no records for the selected section+date: empty state "No attendance recorded for this date."

**Nav item**: Add "Attendance" to the Admin nav (after "Fee Invoices"), with `ClipboardList` icon.

### B5. Status Badge Component

Extract a reusable `AttendanceStatusBadge` component (or inline it):
```tsx
const STATUS_COLORS: Record<AttendanceStatus, string> = {
  Present: 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400',
  Late:    'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
  Absent:  'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
  Excused: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
}
```

---

## Implementation Order

1. Backend: enum → entity → EF config → AppDbContext → repository interface + implementation → `ITeacherRepository.GetByUserIdAsync` addition → DTOs → validator → service → controller → DI registrations → migration
2. Frontend: API client → Teacher Attendance page + route + nav → Admin Attendance view + route + nav

Commit after each logical group (backend complete, frontend API client, Teacher page, Admin page).

---

## Key Invariants

- **Unique constraint** `(SchoolId, StudentId, SectionId, Date)` — enforced at DB level. Re-submitting the same section+date is an update, not a conflict.
- **Teacher ownership check** in `BulkUpsertAsync` — teacher must have a `TeacherSectionSubject` row for `(TeacherId, SectionId, AcademicYearId)`. Throw `DomainException` if not found. Admin cannot call this endpoint at all (role gate).
- **GET endpoints are read-only** (no side effects) per the `SameSite=Lax` CSRF rule in `.claude/rules/backend.md`.
- **`MarkedByUserId`** is set from the JWT `sub` claim in the controller (`User.FindFirstValue(ClaimTypes.NameIdentifier)`), not from the request body.
- **DateOnly serialization**: ASP.NET Core 8 serializes `DateOnly` as `"YYYY-MM-DD"` by default. No custom converter needed. The frontend sends and receives the same format.
