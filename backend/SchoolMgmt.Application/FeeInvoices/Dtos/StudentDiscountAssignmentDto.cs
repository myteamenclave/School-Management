namespace SchoolMgmt.Application.FeeInvoices.Dtos;

public record StudentDiscountAssignmentDto(
    Guid Id, Guid StudentId, Guid DiscountRuleId, string DiscountRuleName,
    string RuleType, decimal Value, Guid AcademicYearId);
