using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class User : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
