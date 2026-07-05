using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.FeeTemplates;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class FeeTemplateRepository(AppDbContext context)
    : Repository<FeeTemplate>(context), IFeeTemplateRepository
{
    public async Task<(List<FeeTemplate> Items, int TotalCount)> GetPagedAsync(
        Guid? academicYearId, Guid? gradeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(t => t.AcademicYear)
            .Include(t => t.Grade)
            .Include(t => t.LineItems)
            .AsQueryable();

        query = isActive.HasValue
            ? query.Where(t => t.IsActive == isActive.Value)
            : query.Where(t => t.IsActive);

        if (academicYearId.HasValue)
            query = query.Where(t => t.AcademicYearId == academicYearId.Value);
        if (gradeId.HasValue)
            query = query.Where(t => t.GradeId == gradeId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.Grade.DisplayOrder)
            .ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<FeeTemplate?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(t => t.AcademicYear)
            .Include(t => t.Grade)
            .Include(t => t.LineItems.OrderBy(li => li.DisplayOrder))
            .Include(t => t.Installments.OrderBy(i => i.DisplayOrder))
            .Include(t => t.DiscountRules)
                .ThenInclude(dr => dr.FeeLineItem)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }
}
