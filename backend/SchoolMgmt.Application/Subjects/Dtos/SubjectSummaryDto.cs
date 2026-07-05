namespace SchoolMgmt.Application.Subjects.Dtos;

public record SubjectSummaryDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt
);
