using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class Semester : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; private set; }

    public void SetCurrent(bool value) => IsCurrent = value;
}
