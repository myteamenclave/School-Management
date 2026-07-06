namespace SchoolMgmt.Application.Enrollments.Dtos;

public record CreateEnrollmentRequest(
    Guid StudentId,
    Guid AcademicYearId
);
