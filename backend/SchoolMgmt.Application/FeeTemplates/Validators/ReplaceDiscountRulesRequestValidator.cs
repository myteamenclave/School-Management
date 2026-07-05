using FluentValidation;
using SchoolMgmt.Application.FeeTemplates.Dtos;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.FeeTemplates.Validators;

public class ReplaceDiscountRulesRequestValidator : AbstractValidator<ReplaceDiscountRulesRequest>
{
    public ReplaceDiscountRulesRequestValidator()
    {
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Name).NotEmpty().MaximumLength(200);
            item.RuleFor(i => i.Value).GreaterThan(0);
            item.When(i => i.RuleType == DiscountRuleType.Percentage,
                () => item.RuleFor(i => i.Value).LessThanOrEqualTo(100));
        });
    }
}
