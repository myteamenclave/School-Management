namespace SchoolMgmt.Application.Grades.Dtos;

public record CreateGradeRequest(string Name, int DisplayOrder);
public record UpdateGradeRequest(string Name, int DisplayOrder);
public record CreateSectionRequest(string Name);
public record UpdateSectionRequest(string Name);
