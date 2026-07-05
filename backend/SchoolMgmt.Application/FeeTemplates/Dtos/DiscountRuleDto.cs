using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record DiscountRuleDto(
    Guid Id,
    string Name,
    DiscountRuleType RuleType,
    decimal Value,
    Guid? FeeLineItemId,
    string? FeeLineItemName
);
