using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.IntegrationTests.Fakes;

public class FakeDateTimeProvider(DateTimeOffset utcNow) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
