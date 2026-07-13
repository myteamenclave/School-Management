using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class StudentSectionEnrollmentRepository(AppDbContext context)
    : Repository<StudentSectionEnrollment>(context), IStudentSectionEnrollmentRepository
{
    public async Task<List<StudentSectionEnrollment>> GetBySectionAndYearAsync(
        Guid sectionId, Guid academicYearId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(e => e.Student)
            .Include(e => e.Section).ThenInclude(s => s.Grade)
            .Include(e => e.AcademicYear)
            .Where(e => e.SectionId == sectionId && e.AcademicYearId == academicYearId)
            .OrderBy(e => e.Student.LastName)
            .ThenBy(e => e.Student.FirstName)
            .ToListAsync(ct);
    }

    public async Task<StudentSectionEnrollment?> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.AcademicYearId == academicYearId, ct);
    }

    public async Task<StudentSectionEnrollment?> GetByIdWithDetailsAsync(
        Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(e => e.Student)
            .Include(e => e.Section).ThenInclude(s => s.Grade)
            .Include(e => e.AcademicYear)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public Task<List<Guid>> GetEnrolledStudentIdsForYearAsync(
        Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .Where(e => e.AcademicYearId == academicYearId)
            .Select(e => e.StudentId)
            .ToListAsync(ct);
}
