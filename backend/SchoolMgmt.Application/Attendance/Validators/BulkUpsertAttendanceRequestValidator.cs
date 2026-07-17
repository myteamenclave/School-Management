using FluentValidation;
using SchoolMgmt.Application.Attendance.Dtos;

namespace SchoolMgmt.Application.Attendance.Validators;

public class BulkUpsertAttendanceRequestValidator : AbstractValidator<BulkUpsertAttendanceRequest>
{
    private static readonly string[] ValidStatuses = ["Present", "Late", "Absent", "Excused"];

    public BulkUpsertAttendanceRequestValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Entries).NotEmpty().WithMessage("At least one entry is required.");
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.StudentId).NotEmpty();
            entry.RuleFor(e => e.Status)
                .Must(s => ValidStatuses.Contains(s))
                .WithMessage("Status must be Present, Late, Absent, or Excused.");
            entry.RuleFor(e => e.Notes).MaximumLength(500);
        });
    }
}
