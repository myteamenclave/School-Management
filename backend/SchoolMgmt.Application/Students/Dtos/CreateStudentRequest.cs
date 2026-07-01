namespace SchoolMgmt.Application.Students.Dtos;

public record CreateStudentRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,
    DateOnly EnrollmentDate,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail
);
