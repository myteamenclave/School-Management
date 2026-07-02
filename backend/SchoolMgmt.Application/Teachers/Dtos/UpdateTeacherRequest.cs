namespace SchoolMgmt.Application.Teachers.Dtos;

public record UpdateTeacherRequest(
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly JoiningDate,
    bool IsActive
);
