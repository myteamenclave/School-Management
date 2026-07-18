using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Gradebook;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class GradeScaleBandRepository(AppDbContext context)
    : Repository<GradeScaleBand>(context), IGradeScaleBandRepository
{
    public Task<List<GradeScaleBand>> GetAllOrderedAsync(CancellationToken ct = default) =>
        DbSet.OrderByDescending(b => b.MinScore).ToListAsync(ct);
}
