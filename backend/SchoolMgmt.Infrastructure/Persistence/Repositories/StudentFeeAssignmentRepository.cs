using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class StudentFeeAssignmentRepository(AppDbContext context)
    : Repository<StudentFeeAssignment>(context), IStudentFeeAssignmentRepository
{
    public Task<StudentFeeAssignment?> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.AcademicYearId == academicYearId, ct);

    public Task<List<StudentFeeAssignment>> GetByTemplateAsync(
        Guid templateId, CancellationToken ct = default) =>
        DbSet
            .Where(a => a.FeeTemplateId == templateId)
            .ToListAsync(ct);

    public Task<List<StudentFeeAssignment>> GetByGradeAndYearAsync(
        Guid gradeId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .Include(a => a.Student)
            .Include(a => a.FeeTemplate)
            .Where(a => a.FeeTemplate.GradeId == gradeId && a.AcademicYearId == academicYearId)
            .ToListAsync(ct);
}
