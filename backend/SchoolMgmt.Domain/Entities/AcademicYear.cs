using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Domain.Entities;

public class AcademicYear : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AcademicYearStatus Status { get; private set; } = AcademicYearStatus.Active;
    public bool IsCurrent { get; private set; }

    private readonly List<Semester> _semesters = [];
    public IReadOnlyList<Semester> Semesters => _semesters.AsReadOnly();

    public void SetCurrent(bool value) => IsCurrent = value;

    public void Archive()
    {
        if (IsCurrent)
            throw new DomainException("Cannot archive the current academic year. Set a different year as current first.");
        Status = AcademicYearStatus.Archived;
    }

    public void EnsureNotArchived()
    {
        if (Status == AcademicYearStatus.Archived)
            throw new DomainException("Cannot modify data in an archived academic year.");
    }

    public static AcademicYear Create(string name, DateOnly startDate, DateOnly endDate)
    {
        var year = new AcademicYear
        {
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
        };

        var totalDays = endDate.DayNumber - startDate.DayNumber;
        var midDate = startDate.AddDays(totalDays / 2);

        year._semesters.Add(new Semester
        {
            Name = "Semester 1",
            AcademicYear = year,
            StartDate = startDate,
            EndDate = midDate,
        });
        year._semesters.Add(new Semester
        {
            Name = "Semester 2",
            AcademicYear = year,
            StartDate = midDate.AddDays(1),
            EndDate = endDate,
        });
        return year;
    }
}
