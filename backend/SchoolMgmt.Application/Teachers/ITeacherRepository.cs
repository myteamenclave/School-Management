using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Teachers;

public interface ITeacherRepository : IRepository<Teacher>
{
    Task<(List<Teacher> Items, int TotalCount)> GetPagedAsync(
        bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default);

    Task<Teacher?> GetByIdWithUserAsync(Guid id, CancellationToken ct = default);

    Task<string> GetNextTeacherCodeAsync(int joiningYear, CancellationToken ct = default);
}
