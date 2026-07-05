using FluentValidation;
using SchoolMgmt.Application.FeeTemplates.Dtos;

namespace SchoolMgmt.Application.FeeTemplates.Validators;

public class ReplaceLineItemsRequestValidator : AbstractValidator<ReplaceLineItemsRequest>
{
    public ReplaceLineItemsRequestValidator()
    {
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Name).NotEmpty().MaximumLength(200);
            item.RuleFor(i => i.Amount).GreaterThan(0);
        });
    }
}
