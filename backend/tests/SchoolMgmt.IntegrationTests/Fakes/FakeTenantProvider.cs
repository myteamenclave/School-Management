using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.IntegrationTests.Fakes;

public class FakeTenantProvider(Guid schoolId) : ITenantProvider
{
    public Guid CurrentSchoolId { get; } = schoolId;
}
