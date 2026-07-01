using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class Section : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}
