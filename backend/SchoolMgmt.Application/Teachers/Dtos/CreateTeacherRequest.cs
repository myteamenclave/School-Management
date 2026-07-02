namespace SchoolMgmt.Application.Teachers.Dtos;

public record CreateTeacherRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly JoiningDate
);
