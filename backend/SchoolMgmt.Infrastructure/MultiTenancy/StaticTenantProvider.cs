using Microsoft.Extensions.Options;
using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.Infrastructure.MultiTenancy;

// Placeholder until Auth (specs/02-implement-auth.md) lands and resolves the tenant from claims.
internal sealed class StaticTenantProvider(IOptions<SeedDataOptions> seedOptions) : ITenantProvider
{
    public Guid CurrentSchoolId => seedOptions.Value.DefaultSchoolId;
}
