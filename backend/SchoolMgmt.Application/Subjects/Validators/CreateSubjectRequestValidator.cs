using FluentValidation;
using SchoolMgmt.Application.Subjects.Dtos;

namespace SchoolMgmt.Application.Subjects.Validators;

public class CreateSubjectRequestValidator : AbstractValidator<CreateSubjectRequest>
{
    public CreateSubjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20)
            .Matches(@"^[A-Za-z0-9_\-]+$").WithMessage("Code must contain only letters, numbers, hyphens, or underscores.");
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
