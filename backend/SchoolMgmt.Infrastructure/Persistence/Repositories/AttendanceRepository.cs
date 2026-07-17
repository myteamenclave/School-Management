using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Attendance;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class AttendanceRepository(AppDbContext context)
    : Repository<AttendanceRecord>(context), IAttendanceRepository
{
    public Task<List<AttendanceRecord>> GetBySectionAndDateAsync(
        Guid sectionId, DateOnly date, CancellationToken ct = default) =>
        DbSet
            .Include(r => r.Student)
            .Where(r => r.SectionId == sectionId && r.Date == date)
            .ToListAsync(ct);

    public Task<AttendanceRecord?> GetByStudentSectionAndDateAsync(
        Guid studentId, Guid sectionId, DateOnly date, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(
            r => r.StudentId == studentId && r.SectionId == sectionId && r.Date == date, ct);

    public Task<List<AttendanceRecord>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .Include(r => r.Section)
            .Where(r => r.StudentId == studentId && r.AcademicYearId == academicYearId)
            .OrderBy(r => r.Date)
            .ToListAsync(ct);
}
