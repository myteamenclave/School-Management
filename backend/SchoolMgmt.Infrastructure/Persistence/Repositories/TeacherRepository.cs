using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Teachers;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class TeacherRepository(AppDbContext context)
    : Repository<Teacher>(context), ITeacherRepository
{
    public async Task<(List<Teacher> Items, int TotalCount)> GetPagedAsync(
        bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.Include(t => t.User).AsQueryable();
        query = isActive.HasValue
            ? query.Where(t => t.IsActive == isActive.Value)
            : query.Where(t => t.IsActive);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Teacher?> GetByIdWithUserAsync(Guid id, CancellationToken ct = default)
        => await DbSet.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<string> GetNextTeacherCodeAsync(int joiningYear, CancellationToken ct = default)
    {
        var prefix = $"{joiningYear}-";
        var maxCode = await DbSet
            .Where(t => t.TeacherCode.StartsWith(prefix))
            .MaxAsync(t => (string?)t.TeacherCode, ct);

        var next = 1;
        if (maxCode is not null)
        {
            var parts = maxCode.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var seq))
                next = seq + 1;
        }

        return $"{joiningYear}-{next:D6}";
    }
}
