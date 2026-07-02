namespace SchoolMgmt.Application.Teachers.Dtos;

public record TeacherDto(
    Guid Id,
    string TeacherCode,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly JoiningDate,
    bool IsActive,
    string Email,
    Guid UserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
