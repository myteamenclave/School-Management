using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Tests.Auth.Fakes;

// Mirrors the real RefreshTokenRepository's .Include(rt => rt.User) by
// resolving the User navigation property from the given FakeUserRepository —
// AuthService never sets RefreshToken.User itself (only UserId), exactly
// like the real EF-backed repository.
public class FakeRefreshTokenRepository(FakeUserRepository users) : IRefreshTokenRepository
{
    private readonly Dictionary<Guid, RefreshToken> _byId = new();

    public IReadOnlyCollection<RefreshToken> All => _byId.Values.ToList();

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var token = _byId.Values.FirstOrDefault(t => t.TokenHash == tokenHash);
        if (token is not null)
            token.User = (await users.GetByIdAsync(token.UserId, cancellationToken))!;
        return token;
    }

    public Task<List<RefreshToken>> GetActiveBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.Values.Where(t => t.SessionId == sessionId && t.RevokedAt == null).ToList());

    public Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    public Task AddAsync(RefreshToken entity, CancellationToken cancellationToken = default)
    {
        _byId[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public void Update(RefreshToken entity) => _byId[entity.Id] = entity;

    public void Remove(RefreshToken entity) => _byId.Remove(entity.Id);
}
