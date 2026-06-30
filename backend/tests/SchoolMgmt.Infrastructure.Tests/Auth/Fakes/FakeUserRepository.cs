using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Tests.Auth.Fakes;

public class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _byId = new();

    public void Seed(User user) => _byId[user.Id] = user;

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.Values.FirstOrDefault(u => u.Email == email));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    public Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        _byId[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public void Update(User entity) => _byId[entity.Id] = entity;

    public void Remove(User entity) => _byId.Remove(entity.Id);
}
