using FluentValidation;
using SchoolMgmt.Application.Enrollments.Dtos;

namespace SchoolMgmt.Application.Enrollments.Validators;

public class CreateEnrollmentRequestValidator : AbstractValidator<CreateEnrollmentRequest>
{
    public CreateEnrollmentRequestValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.AcademicYearId).NotEmpty();
    }
}
