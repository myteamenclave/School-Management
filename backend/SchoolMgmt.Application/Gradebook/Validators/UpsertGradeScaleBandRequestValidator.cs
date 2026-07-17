using FluentValidation;
using SchoolMgmt.Application.Gradebook.Dtos;

namespace SchoolMgmt.Application.Gradebook.Validators;

public class UpsertGradeScaleBandRequestValidator : AbstractValidator<UpsertGradeScaleBandRequest>
{
    public UpsertGradeScaleBandRequestValidator()
    {
        RuleFor(x => x.Letter).NotEmpty().MaximumLength(4);
        RuleFor(x => x.MinScore).InclusiveBetween(0m, 100m);
        RuleFor(x => x.MaxScore).InclusiveBetween(0m, 100m);
        RuleFor(x => x)
            .Must(x => x.MinScore <= x.MaxScore)
            .WithMessage("MinScore must be less than or equal to MaxScore.");
    }
}
