using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    // Bypasses the tenant query filter — refresh happens before a tenant is
    // resolvable (the access token may be expired/absent). Includes the
    // related User so AuthService doesn't need a second tenant-bypassing
    // lookup. See specs/02-implement-auth.md "The pre-authentication tenant
    // problem".
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    // Same bypass rationale — used to revoke an entire session family when
    // token-reuse (theft) is detected.
    Task<List<RefreshToken>> GetActiveBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
