namespace SchoolMgmt.Application.ParentPortal.Dtos;

// Minimal year list for the parent year-selector — NOT the admin AcademicYearDto
// (no semesters, no status internals). Read-only, parent-scoped.
public record ParentAcademicYearDto(Guid Id, string Name, bool IsCurrent);
