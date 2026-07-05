using FluentValidation;
using SchoolMgmt.Application.Subjects.Dtos;

namespace SchoolMgmt.Application.Subjects.Validators;

public class UpdateSubjectRequestValidator : AbstractValidator<UpdateSubjectRequest>
{
    public UpdateSubjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
