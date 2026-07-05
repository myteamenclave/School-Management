using FluentValidation;
using SchoolMgmt.Application.FeeTemplates.Dtos;

namespace SchoolMgmt.Application.FeeTemplates.Validators;

public class CreateFeeTemplateRequestValidator : AbstractValidator<CreateFeeTemplateRequest>
{
    public CreateFeeTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.GradeId).NotEmpty();
    }
}
