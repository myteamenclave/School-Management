namespace SchoolMgmt.Application.Gradebook.Dtos;

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

public record GradeScaleBandDto(Guid Id, string Letter, decimal MinScore, decimal MaxScore);

public record UpsertGradeScaleBandRequest(string Letter, decimal MinScore, decimal MaxScore);
