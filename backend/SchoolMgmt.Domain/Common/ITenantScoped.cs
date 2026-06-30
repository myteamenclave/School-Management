namespace SchoolMgmt.Domain.Common;

public interface ITenantScoped
{
    Guid SchoolId { get; set; }
}
