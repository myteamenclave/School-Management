using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Subjects;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class SubjectRepository(AppDbContext context)
    : Repository<Subject>(context), ISubjectRepository
{
    public async Task<(List<Subject> Items, int TotalCount)> GetPagedAsync(
        bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, pattern) ||
                EF.Functions.ILike(s.Code, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
