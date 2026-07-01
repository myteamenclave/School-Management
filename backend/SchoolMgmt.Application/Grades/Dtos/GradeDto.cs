namespace SchoolMgmt.Application.Grades.Dtos;

public record SectionDto(Guid Id, Guid GradeId, string Name);

public record GradeDto(Guid Id, string Name, int DisplayOrder, List<SectionDto> Sections);
