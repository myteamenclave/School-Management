using FluentValidation;
using SchoolMgmt.Application.FeeTemplates.Dtos;

namespace SchoolMgmt.Application.FeeTemplates.Validators;

public class UpdateFeeTemplateRequestValidator : AbstractValidator<UpdateFeeTemplateRequest>
{
    public UpdateFeeTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
