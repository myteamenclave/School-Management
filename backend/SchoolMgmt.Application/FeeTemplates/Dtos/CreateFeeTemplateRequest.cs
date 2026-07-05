namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record CreateFeeTemplateRequest(
    string Name,
    Guid AcademicYearId,
    Guid GradeId
);
