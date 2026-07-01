namespace SchoolMgmt.Application.AcademicYears.Dtos;

public record SemesterDto(
    Guid Id,
    Guid AcademicYearId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent);

public record AcademicYearDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    bool IsCurrent,
    List<SemesterDto> Semesters);
