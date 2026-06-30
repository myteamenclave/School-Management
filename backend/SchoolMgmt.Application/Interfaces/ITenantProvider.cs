namespace SchoolMgmt.Application.Interfaces;

public interface ITenantProvider
{
    Guid CurrentSchoolId { get; }
}
