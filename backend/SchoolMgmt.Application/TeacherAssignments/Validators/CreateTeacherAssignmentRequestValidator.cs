using FluentValidation;
using SchoolMgmt.Application.TeacherAssignments.Dtos;

namespace SchoolMgmt.Application.TeacherAssignments.Validators;

public class CreateTeacherAssignmentRequestValidator : AbstractValidator<CreateTeacherAssignmentRequest>
{
    public CreateTeacherAssignmentRequestValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.AcademicYearId).NotEmpty();
    }
}
