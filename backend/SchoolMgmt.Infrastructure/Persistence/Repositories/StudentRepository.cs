using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Students;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class StudentRepository(AppDbContext context)
    : Repository<Student>(context), IStudentRepository
{
    public async Task<(List<Student> Items, int TotalCount)> GetPagedAsync(
        EnrollmentStatus? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable();
        query = status.HasValue
            ? query.Where(s => s.EnrollmentStatus == status.Value)
            : query.Where(s => s.EnrollmentStatus == EnrollmentStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.FirstName + " " + s.LastName, pattern) ||
                EF.Functions.ILike(s.StudentCode, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<string> GetNextStudentCodeAsync(int enrollmentYear, CancellationToken ct = default)
    {
        var prefix = $"{enrollmentYear}-";
        var maxCode = await DbSet
            .Where(s => s.StudentCode.StartsWith(prefix))
            .MaxAsync(s => (string?)s.StudentCode, ct);

        var next = 1;
        if (maxCode is not null)
        {
            var parts = maxCode.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var seq))
                next = seq + 1;
        }

        return $"{enrollmentYear}-{next:D6}";
    }
}
