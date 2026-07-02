namespace SchoolMgmt.Application.Teachers.Dtos;

public record TeacherSummaryDto(
    Guid Id,
    string TeacherCode,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly JoiningDate,
    bool IsActive,
    string Email
);
