using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Infrastructure.Tests.TestSupport;

// No real business entity exists yet (out of scope for specs/01) — this
// stand-in lets unit tests exercise the ITenantScoped stamping path on a
// real AppDbContext instance.
public class ProbeEntity : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
}
