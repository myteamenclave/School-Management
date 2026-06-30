using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.Infrastructure.Common;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
