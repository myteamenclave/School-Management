namespace SchoolMgmt.Application.Enrollments.Dtos;

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
