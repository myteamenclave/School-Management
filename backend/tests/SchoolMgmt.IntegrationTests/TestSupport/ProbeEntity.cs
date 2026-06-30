using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.IntegrationTests.TestSupport;

// No real business entity exists yet (out of scope for specs/01) — this
// stand-in lets integration tests exercise tenant isolation and the
// repository/unit-of-work pattern against a real Postgres instance.
public class ProbeEntity : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
}
