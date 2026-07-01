namespace SchoolMgmt.Application.Students.Dtos;

public record UpdateStudentRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,
    DateOnly EnrollmentDate,
    string EnrollmentStatus,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail
);
