using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record ReplaceDiscountRulesRequest(
    IReadOnlyList<DiscountRuleInput> Items
);

public record DiscountRuleInput(
    string Name,
    DiscountRuleType RuleType,
    decimal Value,
    Guid? FeeLineItemId
);
