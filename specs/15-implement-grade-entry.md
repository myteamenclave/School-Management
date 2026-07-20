# Spec 15 — Implement Grade Entry Per Subject/Term (Backend + Frontend)

## Related Docs & Prior Specs

- **Idea doc**: [docs/ideas/12-grade-entry.md](../docs/ideas/12-grade-entry.md)
- **Attendance** (closest prior art — teacher-scoped bulk upsert, admin read-only): [specs/14-implement-attendance-marking.md](14-implement-attendance-marking.md)
- **Academic year / term** (Semester = the "term"; `EnsureNotArchived` guard): [specs/03-implement-academic-year-term-configuration.md](03-implement-academic-year-term-configuration.md)
- **Student section enrollment** (roster source): [specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md)
- **Teacher assignments** (authorization anchor): [specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md)
- **Multi-tenant base**: [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md)

## Overview

Teachers enter three fixed component scores — **Midterm / Final / Coursework** — for each student in a subject they teach, for a chosen semester (the "term"). The server rolls the components into a `TermScore` using a **fixed weight constant** (30 / 40 / 30) and maps it to a `LetterGrade` via an **admin-editable `GradeScale`** band table. Entry is teacher-scoped and bulk-upserted like attendance — freely re-editable, latest value wins, no lock. Admin gets a read-only gradebook plus CRUD over the grade scale.

**Naming note:** `Grade`/`GradeService`/`Application/Grades` already denote *grade-levels* (Grade 5, sections). This feature is namespaced **`Gradebook`** with the entity **`SubjectTermGrade`** to avoid any collision.

**Identity note:** a grade is keyed `(SchoolId, StudentId, SubjectId, SemesterId)`. `SectionId` is stored as **provenance only** (which section it was entered under) — it is *not* part of the unique key. This makes averaging and lookup section-independent, so a mid-term section transfer never fragments or duplicates a grade.

---

## Part A — Backend

### A1. Domain Constant: `GradeWeights`

```csharp
// Domain/Gradebook/GradeWeights.cs
namespace SchoolMgmt.Domain.Gradebook;

// Within-subject term rollup weights. NOT cross-subject GPA weighting.
// Fixed school-wide constant for the demo (idea doc 12 — configurable per-subject is Not Doing).
public static class GradeWeights
{
    public const decimal Midterm    = 0.30m;
    public const decimal Final      = 0.40m;
    public const decimal Coursework = 0.30m;
}
```

### A2. New Domain Entity: `SubjectTermGrade`

```csharp
// Domain/Entities/SubjectTermGrade.cs
public class SubjectTermGrade : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    // Provenance only — which section context the grade was entered under.
    // NOT part of the unique key. Section transfers do not fragment the grade.
    public Guid SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public Guid SemesterId { get; set; }
    public Semester Semester { get; set; } = null!;

    public decimal? MidtermScore { get; private set; }
    public decimal? FinalScore { get; private set; }
    public decimal? CourseworkScore { get; private set; }

    // Auto-computed. Null until ALL three components are present (idea doc 12 — null-until-complete).
    public decimal? TermScore { get; private set; }
    public string? LetterGrade { get; private set; }

    public string? Notes { get; set; }

    public Guid EnteredByUserId { get; set; }
    public User EnteredByUser { get; set; } = null!;

    // Sets components and recomputes TermScore. LetterGrade is applied separately
    // by the service (needs the school's GradeScale bands). See ApplyLetter.
    public void SetScores(decimal? midterm, decimal? final, decimal? coursework)
    {
        MidtermScore = midterm;
        FinalScore = final;
        CourseworkScore = coursework;

        TermScore = (midterm.HasValue && final.HasValue && coursework.HasValue)
            ? Math.Round(
                midterm.Value * GradeWeights.Midterm
                + final.Value * GradeWeights.Final
                + coursework.Value * GradeWeights.Coursework, 2)
            : null;
        LetterGrade = null; // reset — service re-applies from TermScore
    }

    public void ApplyLetter(string? letter) => LetterGrade = letter;
}
```

### A3. New Domain Entity: `GradeScaleBand`

Admin-editable letter bands (idea doc 12). Ordered highest-first; a score maps to the first band whose `[MinScore, MaxScore]` contains it.

```csharp
// Domain/Entities/GradeScaleBand.cs
public class GradeScaleBand : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Letter { get; set; } = string.Empty;   // "A", "B+", ...
    public decimal MinScore { get; set; }                 // inclusive
    public decimal MaxScore { get; set; }                 // inclusive
}
```

> **Scope decision:** the idea doc's `GradeScale` is realized as a flat set of `GradeScaleBand` rows per school (no parent wrapper entity needed for the demo). If a "grade scale" grouping is ever required, it can wrap these rows later.

### A4. EF Core Configuration

```csharp
// Infrastructure/Persistence/Configurations/SubjectTermGradeConfiguration.cs
public class SubjectTermGradeConfiguration : IEntityTypeConfiguration<SubjectTermGrade>
{
    public void Configure(EntityTypeBuilder<SubjectTermGrade> builder)
    {
        builder.ToTable("SubjectTermGrades");

        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.LetterGrade).HasMaxLength(4);
        builder.Property(e => e.MidtermScore).HasPrecision(5, 2);
        builder.Property(e => e.FinalScore).HasPrecision(5, 2);
        builder.Property(e => e.CourseworkScore).HasPrecision(5, 2);
        builder.Property(e => e.TermScore).HasPrecision(5, 2);

        // Section is NOT in the key — identity is student + subject + semester.
        builder.HasIndex(e => new { e.SchoolId, e.StudentId, e.SubjectId, e.SemesterId }).IsUnique();

        builder.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Subject).WithMany().HasForeignKey(e => e.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AcademicYear).WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Semester).WithMany().HasForeignKey(e => e.SemesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EnteredByUser).WithMany().HasForeignKey(e => e.EnteredByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

```csharp
// Infrastructure/Persistence/Configurations/GradeScaleBandConfiguration.cs
public class GradeScaleBandConfiguration : IEntityTypeConfiguration<GradeScaleBand>
{
    public void Configure(EntityTypeBuilder<GradeScaleBand> builder)
    {
        builder.ToTable("GradeScaleBands");
        builder.Property(e => e.Letter).HasMaxLength(4).IsRequired();
        builder.Property(e => e.MinScore).HasPrecision(5, 2);
        builder.Property(e => e.MaxScore).HasPrecision(5, 2);
        builder.HasIndex(e => new { e.SchoolId, e.Letter }).IsUnique();

        // Seed default bands for the seed school, same HasData-everywhere pattern as
        // SchoolConfiguration. Follow that file EXACTLY: instantiate SeedDataOptions for
        // DefaultSchoolId, and seed ANONYMOUS objects (not GradeScaleBand instances) so the
        // private-set BaseEntity.CreatedAt can be given a static value via EF's property-bag
        // mapping. Use hardcoded static Guids for the band ids (no Guid.NewGuid() in HasData).
        // Default scale: A 90-100, B 80-89.99, C 70-79.99, D 60-69.99, F 0-59.99.
        var defaults = new SeedDataOptions();
        var seededAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            new { Id = new Guid("<static-guid-1>"), SchoolId = defaults.DefaultSchoolId, Letter = "A", MinScore = 90m,  MaxScore = 100m,   CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null },
            new { Id = new Guid("<static-guid-2>"), SchoolId = defaults.DefaultSchoolId, Letter = "B", MinScore = 80m,  MaxScore = 89.99m, CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null },
            new { Id = new Guid("<static-guid-3>"), SchoolId = defaults.DefaultSchoolId, Letter = "C", MinScore = 70m,  MaxScore = 79.99m, CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null },
            new { Id = new Guid("<static-guid-4>"), SchoolId = defaults.DefaultSchoolId, Letter = "D", MinScore = 60m,  MaxScore = 69.99m, CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null },
            new { Id = new Guid("<static-guid-5>"), SchoolId = defaults.DefaultSchoolId, Letter = "F", MinScore = 0m,   MaxScore = 59.99m, CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null }
        );
    }
}
```

> Mirror `SchoolConfiguration` (specs/01): `SeedDataOptions` from `SchoolMgmt.Infrastructure.MultiTenancy`, anonymous-object seeding for the private-set `CreatedAt`, and static Guids generated once and pasted in.

### A5. AppDbContext Additions

```csharp
public DbSet<SubjectTermGrade> SubjectTermGrades { get; set; }
public DbSet<GradeScaleBand> GradeScaleBands { get; set; }
```

Register both configurations in `OnModelCreating`:
```csharp
modelBuilder.ApplyConfiguration(new SubjectTermGradeConfiguration());
modelBuilder.ApplyConfiguration(new GradeScaleBandConfiguration());
```

### A6. Repository Interfaces

```csharp
// Application/Gradebook/ISubjectTermGradeRepository.cs
public interface ISubjectTermGradeRepository : IRepository<SubjectTermGrade>
{
    // Grades for a subject in a semester (joined to student for name/code).
    Task<List<SubjectTermGrade>> GetBySubjectAndSemesterAsync(
        Guid subjectId, Guid semesterId, CancellationToken ct = default);

    // The single grade for a student+subject+semester (identity lookup for upsert).
    Task<SubjectTermGrade?> GetByStudentSubjectSemesterAsync(
        Guid studentId, Guid subjectId, Guid semesterId, CancellationToken ct = default);

    // A student's grades across subjects for a year (parent portal / dashboard / student detail).
    Task<List<SubjectTermGrade>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);
}
```

```csharp
// Application/Gradebook/IGradeScaleBandRepository.cs
public interface IGradeScaleBandRepository : IRepository<GradeScaleBand>
{
    Task<List<GradeScaleBand>> GetAllOrderedAsync(CancellationToken ct = default); // MinScore desc
}
```

### A7. Repository Implementations

```csharp
// Infrastructure/Persistence/Repositories/SubjectTermGradeRepository.cs
internal sealed class SubjectTermGradeRepository(AppDbContext context)
    : Repository<SubjectTermGrade>(context), ISubjectTermGradeRepository
{
    public Task<List<SubjectTermGrade>> GetBySubjectAndSemesterAsync(
        Guid subjectId, Guid semesterId, CancellationToken ct = default) =>
        DbSet.Where(g => g.SubjectId == subjectId && g.SemesterId == semesterId).ToListAsync(ct);

    public Task<SubjectTermGrade?> GetByStudentSubjectSemesterAsync(
        Guid studentId, Guid subjectId, Guid semesterId, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(
            g => g.StudentId == studentId && g.SubjectId == subjectId && g.SemesterId == semesterId, ct);

    public Task<List<SubjectTermGrade>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet.Include(g => g.Subject).Include(g => g.Semester)
            .Where(g => g.StudentId == studentId && g.AcademicYearId == academicYearId)
            .ToListAsync(ct);
}
```

```csharp
// Infrastructure/Persistence/Repositories/GradeScaleBandRepository.cs
internal sealed class GradeScaleBandRepository(AppDbContext context)
    : Repository<GradeScaleBand>(context), IGradeScaleBandRepository
{
    public Task<List<GradeScaleBand>> GetAllOrderedAsync(CancellationToken ct = default) =>
        DbSet.OrderByDescending(b => b.MinScore).ToListAsync(ct);
}
```

Roster GET reuses `IStudentSectionEnrollmentRepository.GetBySectionAndYearAsync` (already exists) for the student list, and `ITeacherSectionSubjectRepository.GetBySubjectSectionAndYearAsync` (already exists) for the ownership check. `ITeacherRepository.GetByUserIdAsync` was added in spec 14 — reuse it.

### A8. DTOs

```csharp
// Application/Gradebook/Dtos/

// One roster row for a subject-section-semester.
public record GradeRosterEntryDto(
    Guid StudentId,
    string StudentName,
    string StudentCode,
    decimal? MidtermScore,
    decimal? FinalScore,
    decimal? CourseworkScore,
    decimal? TermScore,     // computed; null until all three present
    string? LetterGrade,    // null until TermScore present
    string? Notes
);

public record SubjectGradeRosterDto(
    Guid SectionId,
    string SectionName,
    Guid SubjectId,
    string SubjectName,
    Guid SemesterId,
    string SemesterName,
    List<GradeRosterEntryDto> Entries
);

// Bulk upsert request.
public record BulkUpsertGradesRequest(
    Guid SectionId,
    Guid SubjectId,
    Guid SemesterId,
    List<GradeEntryRequest> Entries
);

public record GradeEntryRequest(
    Guid StudentId,
    decimal? Midterm,
    decimal? Final,
    decimal? Coursework,
    string? Notes
);

public record BulkUpsertGradesResult(int Upserted);

// Student-centric view (parent portal / student detail / dashboard).
public record StudentGradeDto(
    Guid Id,
    Guid SubjectId,
    string SubjectName,
    Guid SemesterId,
    string SemesterName,
    decimal? MidtermScore,
    decimal? FinalScore,
    decimal? CourseworkScore,
    decimal? TermScore,
    string? LetterGrade,
    string? Notes
);

// Grade scale band DTO + CRUD requests.
public record GradeScaleBandDto(Guid Id, string Letter, decimal MinScore, decimal MaxScore);
public record UpsertGradeScaleBandRequest(string Letter, decimal MinScore, decimal MaxScore);
```

### A9. Validators

```csharp
// Application/Gradebook/Validators/BulkUpsertGradesRequestValidator.cs
public class BulkUpsertGradesRequestValidator : AbstractValidator<BulkUpsertGradesRequest>
{
    public BulkUpsertGradesRequestValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.Entries).NotEmpty();
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.StudentId).NotEmpty();
            entry.RuleFor(e => e.Midterm).InclusiveBetween(0m, 100m).When(e => e.Midterm.HasValue);
            entry.RuleFor(e => e.Final).InclusiveBetween(0m, 100m).When(e => e.Final.HasValue);
            entry.RuleFor(e => e.Coursework).InclusiveBetween(0m, 100m).When(e => e.Coursework.HasValue);
            entry.RuleFor(e => e.Notes).MaximumLength(500);
        });
    }
}
```

```csharp
// Application/Gradebook/Validators/UpsertGradeScaleBandRequestValidator.cs
public class UpsertGradeScaleBandRequestValidator : AbstractValidator<UpsertGradeScaleBandRequest>
{
    public UpsertGradeScaleBandRequestValidator()
    {
        RuleFor(x => x.Letter).NotEmpty().MaximumLength(4);
        RuleFor(x => x.MinScore).InclusiveBetween(0m, 100m);
        RuleFor(x => x.MaxScore).InclusiveBetween(0m, 100m);
        RuleFor(x => x).Must(x => x.MinScore <= x.MaxScore)
            .WithMessage("MinScore must be less than or equal to MaxScore.");
    }
}
```

### A10. Letter Resolution Helper

```csharp
// Application/Gradebook/LetterResolver.cs
internal static class LetterResolver
{
    // bands must be ordered MinScore desc. Returns first band containing the score.
    public static string? Resolve(IReadOnlyList<GradeScaleBand> bands, decimal? score) =>
        score is null ? null
        : bands.FirstOrDefault(b => score >= b.MinScore && score <= b.MaxScore)?.Letter;
}
```

### A11. GradebookService

```csharp
// Application/Gradebook/GradebookService.cs
public class GradebookService(
    ISubjectTermGradeRepository gradeRepo,
    IGradeScaleBandRepository bandRepo,
    IStudentSectionEnrollmentRepository enrollmentRepo,
    ITeacherSectionSubjectRepository assignmentRepo,
    ITeacherRepository teacherRepo,
    IAcademicYearRepository yearRepo,
    IRepository<Section> sectionRepo,
    IRepository<Subject> subjectRepo,
    IUnitOfWork unitOfWork)
{
    // GET roster — enrolled students for section+year with their grade (by student+subject+semester).
    // Teacher and Admin. Students come from CURRENT enrollment; grades join by student identity,
    // so a transferred student's grade (entered under a different section) still shows here.
    public async Task<SubjectGradeRosterDto> GetSubjectRosterAsync(
        Guid sectionId, Guid subjectId, Guid semesterId, CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(sectionId, ct) ?? throw new NotFoundException("Section not found.");
        var subject = await subjectRepo.GetByIdAsync(subjectId, ct) ?? throw new NotFoundException("Subject not found.");
        var semester = await yearRepo.GetSemesterByIdAsync(semesterId, ct) ?? throw new NotFoundException("Semester not found.");

        var enrollments = await enrollmentRepo.GetBySectionAndYearAsync(sectionId, semester.AcademicYearId, ct);
        var grades = await gradeRepo.GetBySubjectAndSemesterAsync(subjectId, semesterId, ct);
        var gradeByStudent = grades.ToDictionary(g => g.StudentId);

        var entries = enrollments.Select(e =>
        {
            gradeByStudent.TryGetValue(e.StudentId, out var g);
            return new GradeRosterEntryDto(
                e.StudentId,
                $"{e.Student.FirstName} {e.Student.LastName}",
                e.Student.StudentCode,
                g?.MidtermScore, g?.FinalScore, g?.CourseworkScore,
                g?.TermScore, g?.LetterGrade, g?.Notes);
        }).ToList();

        return new SubjectGradeRosterDto(
            sectionId, section.Name, subjectId, subject.Name, semesterId, semester.Name, entries);
    }

    // PUT bulk — Teacher only. Validates the caller owns the (subject, section, year) slot
    // and the year is not archived.
    public async Task<BulkUpsertGradesResult> BulkUpsertAsync(
        BulkUpsertGradesRequest request, Guid enteredByUserId, CancellationToken ct = default)
    {
        var semester = await yearRepo.GetSemesterByIdAsync(request.SemesterId, ct)
            ?? throw new NotFoundException("Semester not found.");
        var year = await yearRepo.GetByIdAsync(semester.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year not found.");
        year.EnsureNotArchived();

        var teacher = await teacherRepo.GetByUserIdAsync(enteredByUserId, ct)
            ?? throw new NotFoundException("Teacher profile not found.");
        var assignment = await assignmentRepo.GetBySubjectSectionAndYearAsync(
            request.SubjectId, request.SectionId, semester.AcademicYearId, ct);
        if (assignment is null || assignment.TeacherId != teacher.Id)
            throw new DomainException("You are not assigned to teach this subject in this section for the selected year.");

        var bands = await bandRepo.GetAllOrderedAsync(ct);

        var upserted = 0;
        foreach (var entry in request.Entries)
        {
            var existing = await gradeRepo.GetByStudentSubjectSemesterAsync(
                entry.StudentId, request.SubjectId, request.SemesterId, ct);

            if (existing is not null)
            {
                existing.SetScores(entry.Midterm, entry.Final, entry.Coursework);
                existing.ApplyLetter(LetterResolver.Resolve(bands, existing.TermScore));
                existing.Notes = entry.Notes;
                existing.SectionId = request.SectionId;       // refresh provenance to current section
                existing.EnteredByUserId = enteredByUserId;
                gradeRepo.Update(existing);
            }
            else
            {
                var grade = new SubjectTermGrade
                {
                    StudentId = entry.StudentId,
                    SubjectId = request.SubjectId,
                    SectionId = request.SectionId,
                    AcademicYearId = semester.AcademicYearId,
                    SemesterId = request.SemesterId,
                    Notes = entry.Notes,
                    EnteredByUserId = enteredByUserId,
                };
                grade.SetScores(entry.Midterm, entry.Final, entry.Coursework);
                grade.ApplyLetter(LetterResolver.Resolve(bands, grade.TermScore));
                await gradeRepo.AddAsync(grade, ct);
            }
            upserted++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new BulkUpsertGradesResult(upserted);
    }

    // GET student grades for a year — Teacher and Admin.
    public async Task<List<StudentGradeDto>> GetStudentGradesAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var grades = await gradeRepo.GetByStudentAndYearAsync(studentId, academicYearId, ct);
        return grades.Select(g => new StudentGradeDto(
            g.Id, g.SubjectId, g.Subject.Name, g.SemesterId, g.Semester.Name,
            g.MidtermScore, g.FinalScore, g.CourseworkScore, g.TermScore, g.LetterGrade, g.Notes)).ToList();
    }
}
```

### A12. GradeScaleService

```csharp
// Application/Gradebook/GradeScaleService.cs
public class GradeScaleService(IGradeScaleBandRepository bandRepo, IUnitOfWork unitOfWork)
{
    public async Task<List<GradeScaleBandDto>> GetAllAsync(CancellationToken ct = default) =>
        (await bandRepo.GetAllOrderedAsync(ct))
            .Select(b => new GradeScaleBandDto(b.Id, b.Letter, b.MinScore, b.MaxScore)).ToList();

    public async Task<GradeScaleBandDto> CreateAsync(UpsertGradeScaleBandRequest req, CancellationToken ct = default)
    {
        var band = new GradeScaleBand { Letter = req.Letter, MinScore = req.MinScore, MaxScore = req.MaxScore };
        await bandRepo.AddAsync(band, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new GradeScaleBandDto(band.Id, band.Letter, band.MinScore, band.MaxScore);
    }

    public async Task<GradeScaleBandDto> UpdateAsync(Guid id, UpsertGradeScaleBandRequest req, CancellationToken ct = default)
    {
        var band = await bandRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Grade scale band not found.");
        band.Letter = req.Letter; band.MinScore = req.MinScore; band.MaxScore = req.MaxScore;
        bandRepo.Update(band);
        await unitOfWork.SaveChangesAsync(ct);
        return new GradeScaleBandDto(band.Id, band.Letter, band.MinScore, band.MaxScore);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var band = await bandRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Grade scale band not found.");
        bandRepo.Remove(band);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

> **Note:** existing grades are **not** retroactively re-lettered when a band is edited. Letters are re-resolved on the next bulk upsert for that subject/semester. Acceptable for the demo; call out in the UI copy ("edits apply to newly saved grades"). Flagged as an open item, not MVP.

### A13. Controllers

```csharp
// WebApi/Controllers/GradesController.cs
[ApiController]
[Route("api/grades")]
[Authorize(Roles = "Admin,Teacher")]
public class GradesController(GradebookService service) : ControllerBase
{
    // GET /api/grades/subject-roster?sectionId=&subjectId=&semesterId=  (Admin + Teacher)
    [HttpGet("subject-roster")]
    public async Task<IActionResult> GetSubjectRoster(
        [FromQuery] Guid sectionId, [FromQuery] Guid subjectId, [FromQuery] Guid semesterId, CancellationToken ct)
        => Ok(await service.GetSubjectRosterAsync(sectionId, subjectId, semesterId, ct));

    // PUT /api/grades/bulk  (Teacher only; ownership + archive checks in service)
    [HttpPut("bulk")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> BulkUpsert([FromBody] BulkUpsertGradesRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await service.BulkUpsertAsync(request, userId, ct));
    }

    // GET /api/grades/student?studentId=&academicYearId=  (Admin + Teacher)
    [HttpGet("student")]
    public async Task<IActionResult> GetStudentGrades(
        [FromQuery] Guid studentId, [FromQuery] Guid academicYearId, CancellationToken ct)
        => Ok(await service.GetStudentGradesAsync(studentId, academicYearId, ct));
}
```

```csharp
// WebApi/Controllers/GradeScaleController.cs
[ApiController]
[Route("api/grade-scale")]
[Authorize(Roles = "Admin")]
public class GradeScaleController(GradeScaleService service) : ControllerBase
{
    [HttpGet]                                   // GET /api/grade-scale (Admin)
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertGradeScaleBandRequest req, CancellationToken ct)
        => Ok(await service.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertGradeScaleBandRequest req, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

Import needed in `GradesController`: `using System.Security.Claims;`

> **GET-scale for Teacher:** `GET /api/grade-scale` is `Admin,Teacher` (writes stay Admin-only). The teacher gradebook fetches the bands and maps the provisional term score to a letter **live** as scores are typed — mirroring the server's `LetterResolver` — so the letter updates before save, not only after reload. (Originally scoped Admin-only; relaxed once the live-letter UX was needed.)

### A14. DI Registration

`Infrastructure/DependencyInjection.cs`:
```csharp
services.AddScoped<ISubjectTermGradeRepository, SubjectTermGradeRepository>();
services.AddScoped<IGradeScaleBandRepository, GradeScaleBandRepository>();
```

`Application/DependencyInjection.cs`:
```csharp
services.AddScoped<GradebookService>();
services.AddScoped<GradeScaleService>();
```

### A15. Migration

```bash
dotnet ef migrations add AddGradebook \
  --project SchoolMgmt.Infrastructure \
  --startup-project SchoolMgmt.WebApi
```

Verify the migration includes the `HasData` seed rows for `GradeScaleBands`.

---

## Part B — Frontend

### B1. API Client

```ts
// frontend/src/api/grades.ts
import { api } from './client'

export interface GradeRosterEntry {
  studentId: string
  studentName: string
  studentCode: string
  midtermScore: number | null
  finalScore: number | null
  courseworkScore: number | null
  termScore: number | null
  letterGrade: string | null
  notes: string | null
}

export interface SubjectGradeRoster {
  sectionId: string; sectionName: string
  subjectId: string; subjectName: string
  semesterId: string; semesterName: string
  entries: GradeRosterEntry[]
}

export interface GradeEntryRequest {
  studentId: string
  midterm: number | null
  final: number | null
  coursework: number | null
  notes?: string | null
}

export interface BulkUpsertGradesRequest {
  sectionId: string; subjectId: string; semesterId: string
  entries: GradeEntryRequest[]
}

export interface StudentGrade {
  id: string
  subjectId: string; subjectName: string
  semesterId: string; semesterName: string
  midtermScore: number | null
  finalScore: number | null
  courseworkScore: number | null
  termScore: number | null
  letterGrade: string | null
  notes: string | null
}

export interface GradeScaleBand { id: string; letter: string; minScore: number; maxScore: number }
export interface UpsertGradeScaleBandRequest { letter: string; minScore: number; maxScore: number }

export const GRADE_KEYS = {
  subjectRoster: (sectionId: string, subjectId: string, semesterId: string) =>
    ['grades', 'subject-roster', sectionId, subjectId, semesterId] as const,
  studentGrades: (studentId: string, academicYearId: string) =>
    ['grades', 'student', studentId, academicYearId] as const,
  scale: () => ['grade-scale'] as const,
}

export const gradesApi = {
  getSubjectRoster: (sectionId: string, subjectId: string, semesterId: string) =>
    api.get<SubjectGradeRoster>('/grades/subject-roster', { params: { sectionId, subjectId, semesterId } }).then(r => r.data),
  bulkUpsert: (request: BulkUpsertGradesRequest) =>
    api.put<{ upserted: number }>('/grades/bulk', request).then(r => r.data),
  getStudentGrades: (studentId: string, academicYearId: string) =>
    api.get<StudentGrade[]>('/grades/student', { params: { studentId, academicYearId } }).then(r => r.data),
}

export const gradeScaleApi = {
  getAll: () => api.get<GradeScaleBand[]>('/grade-scale').then(r => r.data),
  create: (req: UpsertGradeScaleBandRequest) => api.post<GradeScaleBand>('/grade-scale', req).then(r => r.data),
  update: (id: string, req: UpsertGradeScaleBandRequest) => api.put<GradeScaleBand>(`/grade-scale/${id}`, req).then(r => r.data),
  remove: (id: string) => api.delete(`/grade-scale/${id}`).then(r => r.data),
}
```

### B2. Teacher Gradebook Page

**Route**: `/teacher/gradebook` · **Role**: Teacher only

```
frontend/src/pages/teacher/gradebook/GradebookPage.tsx
```

Structure:
1. **Assignment picker** — fetch the logged-in teacher's `TeacherSectionSubject` slots for the active year (reuse the teacher-assignments query used by the Attendance page). The teacher picks a **subject + section** slot (a slot combines both). Auto-select if only one.
2. **Semester picker** — `Select` of the active year's semesters, defaulting to the current semester (`IsCurrent`).
3. **Roster table** (loads when slot + semester chosen): columns — Student (name + code), Midterm, Final, Coursework (three numeric `<Input type="number" min=0 max=100 step=0.01>`), read-only **Term** and **Letter** columns. Pre-populated from the roster GET. Term/Letter show "—" until all three components are filled and saved (server computes on save).
4. **Save** → `PUT /api/grades/bulk` → success toast; refetch to display server-computed Term/Letter.
5. **Unsaved-changes guard** — `useBlocker` when the grid is dirty (same pattern as `FeeTemplatePage` / Attendance).

> Client may show a *provisional* term preview using the same 30/40/30 weights, but the authoritative Term/Letter always come from the server response (single source of truth for the weight + band logic).

### B3. Teacher Routes + App Shell

Add to the Teacher nav (alongside Attendance from spec 14):
```ts
{ label: 'Gradebook', to: '/teacher/gradebook', icon: <BookOpen size={18} />, roles: ['Teacher'] }
```
Add route `teacher/gradebook` → `GradebookPage`, guarded by the existing `RequireRole("Teacher")` pattern.

### B4. Admin Read-Only Gradebook View

**Route**: `/admin/gradebook`

```
frontend/src/pages/admin/gradebook/GradebookViewPage.tsx
```

Structure: Grade `Select` → Section `Select` (filtered) → Subject `Select` → Semester `Select` → read-only roster table (student, code, midterm, final, coursework, term, letter badge). Empty state when nothing entered. Nav item "Gradebook" with `BookOpen` icon.

### B5. Admin Grade Scale Management Page

**Route**: `/admin/grade-scale`

```
frontend/src/pages/admin/grade-scale/GradeScalePage.tsx
```

Structure: table of bands (Letter, Min, Max) ordered high→low; add / edit (modal with the three fields) / delete. Helper copy: *"Grade scale edits apply to grades saved after the change; existing grades keep their stored letter until re-saved."* Nav item under an Admin "Academic" or "Settings" group.

### B6. Student Detail — Grades Tab (optional, if time permits)

Add a **Grades** tab to the existing Student Detail page (parallels the Fee Assignment tab): `GET /api/grades/student?studentId=&academicYearId=` → table grouped by semester, showing per-subject Term + Letter. This is the read surface the parent portal will later reuse.

### B7. Letter Badge Component

Small reusable badge for the letter grade (neutral palette; letters are school-defined so avoid hardcoding per-letter colors — use a single accent, or a green/amber/red gradient keyed off the numeric term score band A/B vs C/D vs F).

---

## Implementation Order

1. **Backend**: `GradeWeights` → `SubjectTermGrade` + `GradeScaleBand` entities → EF configs (with `HasData` seed) → AppDbContext → repository interfaces + implementations → DTOs → validators → `LetterResolver` → `GradebookService` → `GradeScaleService` → controllers → DI → migration (verify seed rows).
2. **Frontend**: API client → Teacher Gradebook page + route + nav → Admin read-only view + route + nav → Grade Scale management page → (optional) Student Detail Grades tab.

Commit after each logical group (backend complete, frontend API client, teacher page, admin pages).

---

## Key Invariants

- **Unique key `(SchoolId, StudentId, SubjectId, SemesterId)`** — one grade per student per subject per term. `SectionId` is provenance only and is refreshed to the current section on each upsert. Re-submitting is an update, not a conflict.
- **Section-independent identity** — roster GET lists *current* enrollment and joins grades by student identity, so a transferred student's grade remains visible/editable to whoever now teaches the subject. Averages join on `StudentId`, never on section.
- **`TermScore` null until all three components present** (idea doc 12). `LetterGrade` is null whenever `TermScore` is null.
- **Weights are a domain constant** (`GradeWeights` 30/40/30) — within-subject rollup only; there is **no** cross-subject GPA aggregation anywhere.
- **Teacher ownership check** in `BulkUpsertAsync` — the caller must own the `(SubjectId, SectionId, AcademicYearId)` slot via `TeacherSectionSubject`. Admin cannot call the bulk endpoint (role gate). Admin write access is explicitly Not Doing.
- **Archive guard** — `AcademicYear.EnsureNotArchived()` is called before any grade write.
- **`EnteredByUserId`** comes from the JWT `sub` claim (`User.FindFirstValue(ClaimTypes.NameIdentifier)`), never the request body.
- **GET endpoints are read-only** per the `SameSite=Lax` CSRF rule in `.claude/rules/backend.md`.
- **Server is the source of truth** for `TermScore` and `LetterGrade`; the client never persists computed values.
- **Grade scale edits are not retroactive** — existing grades re-letter on next save. Documented limitation, not a bug.

---

## Not in This Spec (per idea doc 12)

Cross-subject GPA / rank / honor roll; configurable per-subject weights; open assessment lists; draft→publish lock; admin grade entry/override; grade change history/audit; report-card export; class distribution/average in the teacher grid (dashboard-only).
