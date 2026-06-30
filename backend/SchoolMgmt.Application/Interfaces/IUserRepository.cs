using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    // Bypasses the tenant query filter — login happens before a tenant is
    // resolvable. See specs/02-implement-auth.md "The pre-authentication
    // tenant problem" (amends specs/01's "never bypass outside tests" rule).
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
