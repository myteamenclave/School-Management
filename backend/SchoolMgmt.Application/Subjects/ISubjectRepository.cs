using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Subjects;

public interface ISubjectRepository : IRepository<Subject>
{
    Task<(List<Subject> Items, int TotalCount)> GetPagedAsync(
        bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default);
}
