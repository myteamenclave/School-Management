namespace SchoolMgmt.Application.Subjects.Dtos;

public record SubjectDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
