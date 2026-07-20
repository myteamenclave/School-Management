using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class StudentParent : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid UserId { get; set; }
    public User ParentUser { get; set; } = null!;
}
