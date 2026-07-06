using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class TeacherSectionSubject : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public Guid SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}
