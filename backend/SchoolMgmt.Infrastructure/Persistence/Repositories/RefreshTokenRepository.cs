using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(AppDbContext context) : Repository<RefreshToken>(context), IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        DbSet.IgnoreQueryFilters()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public Task<List<RefreshToken>> GetActiveBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        DbSet.IgnoreQueryFilters()
            .Where(rt => rt.SessionId == sessionId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);
}
