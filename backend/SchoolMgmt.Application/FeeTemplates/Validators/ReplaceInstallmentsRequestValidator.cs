using FluentValidation;
using SchoolMgmt.Application.FeeTemplates.Dtos;

namespace SchoolMgmt.Application.FeeTemplates.Validators;

public class ReplaceInstallmentsRequestValidator : AbstractValidator<ReplaceInstallmentsRequest>
{
    public ReplaceInstallmentsRequestValidator()
    {
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Name).NotEmpty().MaximumLength(200);
            item.RuleFor(i => i.Percentage).GreaterThan(0).LessThanOrEqualTo(100);
        });
    }
}
