using SchoolMgmt.Application.ParentAccounts;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Tests.ParentAccounts.Fakes;

public class FakeStudentParentRepository : IStudentParentRepository
{
    public List<StudentParent> Links { get; } = new();

    public void Seed(StudentParent link) => Links.Add(link);

    public Task<StudentParent?> GetLinkAsync(Guid studentId, Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Links.FirstOrDefault(l => l.StudentId == studentId && l.UserId == userId));

    public Task<List<StudentParent>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default) =>
        Task.FromResult(Links.Where(l => l.StudentId == studentId).ToList());

    public Task<StudentParent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Links.FirstOrDefault(l => l.Id == id));

    public Task AddAsync(StudentParent entity, CancellationToken cancellationToken = default)
    {
        Links.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(StudentParent entity) { }

    public void Remove(StudentParent entity) => Links.RemoveAll(l => l.Id == entity.Id);
}
