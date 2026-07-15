using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class FeeInvoiceRepository(AppDbContext context)
    : Repository<FeeInvoice>(context), IFeeInvoiceRepository
{
    public async Task<string> GetNextInvoiceCodeAsync(int year, CancellationToken ct = default)
    {
        var prefix = $"INV-{year}-";
        var maxCode = await DbSet
            .Where(i => i.InvoiceCode.StartsWith(prefix))
            .MaxAsync(i => (string?)i.InvoiceCode, ct);

        var next = 1;
        if (maxCode is not null)
        {
            var parts = maxCode.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var seq))
                next = seq + 1;
        }

        return $"INV-{year}-{next:D6}";
    }

    public Task<FeeInvoice?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        DbSet
            .Include(i => i.Student)
            .Include(i => i.FeeTemplate)
            .Include(i => i.AcademicYear)
            .Include(i => i.LineItems)
            .Include(i => i.Installments)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<FeeInvoice?> GetActiveForStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .FirstOrDefaultAsync(i =>
                i.StudentId == studentId &&
                i.AcademicYearId == academicYearId &&
                i.Status != InvoiceStatus.Cancelled, ct);

    public async Task<(List<FeeInvoice> Items, int TotalCount)> GetPagedAsync(
        InvoiceStatus? status, Guid? gradeId, Guid? academicYearId,
        Guid? studentId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(i => i.Student)
            .Include(i => i.FeeTemplate)
            .Include(i => i.AcademicYear)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);
        if (gradeId.HasValue)
            query = query.Where(i => i.FeeTemplate.GradeId == gradeId.Value);
        if (academicYearId.HasValue)
            query = query.Where(i => i.AcademicYearId == academicYearId.Value);
        if (studentId.HasValue)
            query = query.Where(i => i.StudentId == studentId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
