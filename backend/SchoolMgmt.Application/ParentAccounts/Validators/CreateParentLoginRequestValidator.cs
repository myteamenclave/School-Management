using FluentValidation;
using SchoolMgmt.Application.ParentAccounts.Dtos;

namespace SchoolMgmt.Application.ParentAccounts.Validators;

public class CreateParentLoginRequestValidator : AbstractValidator<CreateParentLoginRequest>
{
    public CreateParentLoginRequestValidator()
    {
        RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
