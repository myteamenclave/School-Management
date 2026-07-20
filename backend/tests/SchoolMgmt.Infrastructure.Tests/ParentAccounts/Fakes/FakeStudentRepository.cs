using SchoolMgmt.Application.Students;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Infrastructure.Tests.ParentAccounts.Fakes;

public class FakeStudentRepository : IStudentRepository
{
    private readonly Dictionary<Guid, Student> _byId = new();

    public void Seed(Student student) => _byId[student.Id] = student;

    public Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    public Task AddAsync(Student entity, CancellationToken cancellationToken = default)
    {
        _byId[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public void Update(Student entity) => _byId[entity.Id] = entity;

    public void Remove(Student entity) => _byId.Remove(entity.Id);

    public Task<(List<Student> Items, int TotalCount)> GetPagedAsync(
        EnrollmentStatus? status, string? search, int page, int pageSize, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<string> GetNextStudentCodeAsync(int enrollmentYear, CancellationToken ct = default) =>
        throw new NotSupportedException();
}
