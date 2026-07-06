namespace SchoolMgmt.Application.TeacherAssignments.Dtos;

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
