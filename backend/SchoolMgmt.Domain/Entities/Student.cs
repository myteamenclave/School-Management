using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Domain.Entities;

public class Student : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public DateOnly EnrollmentDate { get; set; }
    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Active;
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianEmail { get; set; }
}
