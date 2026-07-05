namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record FeeTemplateSummaryDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid GradeId,
    string GradeName,
    decimal TotalAmount,
    int LineItemCount,
    bool IsActive,
    DateTimeOffset CreatedAt
);
