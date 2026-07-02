using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class Teacher : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TeacherCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly JoiningDate { get; set; }
    public bool IsActive { get; set; } = true;
}
