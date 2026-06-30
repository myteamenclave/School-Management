using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.Infrastructure.Tests.Fakes;

public class FakeDateTimeProvider(DateTimeOffset utcNow) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}
