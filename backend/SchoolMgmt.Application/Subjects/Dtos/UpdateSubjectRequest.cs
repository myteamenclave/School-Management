namespace SchoolMgmt.Application.Subjects.Dtos;

public record UpdateSubjectRequest(
    string Name,
    string? Description,
    bool IsActive
);
