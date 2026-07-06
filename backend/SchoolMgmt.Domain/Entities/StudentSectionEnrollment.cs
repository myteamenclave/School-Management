using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class StudentSectionEnrollment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}
