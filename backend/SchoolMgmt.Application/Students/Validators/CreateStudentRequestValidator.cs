using FluentValidation;
using SchoolMgmt.Application.Students.Dtos;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.Students.Validators;

public class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.Gender).NotEmpty().IsEnumName(typeof(Gender), caseSensitive: false);
        RuleFor(x => x.EnrollmentDate).NotEmpty();
        RuleFor(x => x.GuardianName).MaximumLength(200).When(x => x.GuardianName is not null);
        RuleFor(x => x.GuardianPhone).MaximumLength(20).When(x => x.GuardianPhone is not null);
        RuleFor(x => x.GuardianEmail)
            .MaximumLength(256).EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.GuardianEmail));
    }
}
