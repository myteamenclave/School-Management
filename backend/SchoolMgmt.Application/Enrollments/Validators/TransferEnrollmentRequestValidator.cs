using FluentValidation;
using SchoolMgmt.Application.Enrollments.Dtos;

namespace SchoolMgmt.Application.Enrollments.Validators;

public class TransferEnrollmentRequestValidator : AbstractValidator<TransferEnrollmentRequest>
{
    public TransferEnrollmentRequestValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
    }
}
