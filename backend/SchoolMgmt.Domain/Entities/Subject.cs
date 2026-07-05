using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class Subject : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
