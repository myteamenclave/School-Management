namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record FeeTemplateDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid GradeId,
    string GradeName,
    decimal TotalAmount,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<FeeLineItemDto> LineItems,
    IReadOnlyList<FeeInstallmentDto> Installments,
    IReadOnlyList<DiscountRuleDto> DiscountRules
);
