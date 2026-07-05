namespace SchoolMgmt.Application.Subjects.Dtos;

public record CreateSubjectRequest(
    string Name,
    string Code,
    string? Description
);
