using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.Infrastructure.Tests.Fakes;

public class FakeTenantProvider(Guid schoolId) : ITenantProvider
{
    public Guid CurrentSchoolId { get; } = schoolId;
}
