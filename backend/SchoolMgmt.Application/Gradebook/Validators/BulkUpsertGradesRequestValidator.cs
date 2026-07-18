using FluentValidation;
using SchoolMgmt.Application.Gradebook.Dtos;

namespace SchoolMgmt.Application.Gradebook.Validators;

public class BulkUpsertGradesRequestValidator : AbstractValidator<BulkUpsertGradesRequest>
{
    public BulkUpsertGradesRequestValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.Entries).NotEmpty();
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.StudentId).NotEmpty();
            entry.RuleFor(e => e.Midterm).InclusiveBetween(0m, 100m).When(e => e.Midterm.HasValue);
            entry.RuleFor(e => e.Final).InclusiveBetween(0m, 100m).When(e => e.Final.HasValue);
            entry.RuleFor(e => e.Coursework).InclusiveBetween(0m, 100m).When(e => e.Coursework.HasValue);
            entry.RuleFor(e => e.Notes).MaximumLength(500);
        });
    }
}
