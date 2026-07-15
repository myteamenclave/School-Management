using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class StudentDiscountAssignmentRepository(AppDbContext context)
    : Repository<StudentDiscountAssignment>(context), IStudentDiscountAssignmentRepository
{
    public Task<List<StudentDiscountAssignment>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .Include(d => d.DiscountRule)
            .Where(d => d.StudentId == studentId && d.AcademicYearId == academicYearId)
            .ToListAsync(ct);

    public Task<StudentDiscountAssignment?> GetByStudentRuleAndYearAsync(
        Guid studentId, Guid discountRuleId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .FirstOrDefaultAsync(d =>
                d.StudentId == studentId &&
                d.DiscountRuleId == discountRuleId &&
                d.AcademicYearId == academicYearId, ct);
}
