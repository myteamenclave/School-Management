namespace SchoolMgmt.Application.Students.Dtos;

public record StudentSummaryDto(
    Guid Id,
    string StudentCode,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,
    DateOnly EnrollmentDate,
    string EnrollmentStatus
);
