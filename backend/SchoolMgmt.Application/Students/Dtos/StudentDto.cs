namespace SchoolMgmt.Application.Students.Dtos;

public record StudentDto(
    Guid Id,
    string StudentCode,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,
    DateOnly EnrollmentDate,
    string EnrollmentStatus,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
