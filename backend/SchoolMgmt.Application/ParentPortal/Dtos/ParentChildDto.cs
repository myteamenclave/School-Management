namespace SchoolMgmt.Application.ParentPortal.Dtos;

// One linked child in the parent portal switcher. CurrentGradeLabel/CurrentSectionName
// are null when the child has no enrollment in the CURRENT academic year (e.g. not yet
// enrolled this year, or withdrawn) — the switcher still lists them.
public record ParentChildDto(
    Guid StudentId,
    string StudentName,          // "First Last"
    string StudentCode,
    string EnrollmentStatus,     // Student.EnrollmentStatus.ToString()
    string? CurrentGradeLabel,   // e.g. "Grade 5" (from current-year enrollment)
    string? CurrentSectionName   // e.g. "A"
);
