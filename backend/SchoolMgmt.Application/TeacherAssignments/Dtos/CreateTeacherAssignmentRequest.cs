namespace SchoolMgmt.Application.TeacherAssignments.Dtos;

public record CreateTeacherAssignmentRequest(
    Guid SubjectId,
    Guid SectionId,
    Guid AcademicYearId
);
