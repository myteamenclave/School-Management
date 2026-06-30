using Microsoft.AspNetCore.Http;
using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.Infrastructure.MultiTenancy;

// Replaces StaticTenantProvider (specs/01). Reads SchoolId from the
// authenticated user's claims — see specs/02-implement-auth.md.
internal sealed class HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public Guid CurrentSchoolId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst("school_id")
                ?? throw new InvalidOperationException(
                    "No authenticated tenant context. CurrentSchoolId must only be accessed on authenticated, tenant-scoped requests.");
            return Guid.Parse(claim.Value);
        }
    }
}
