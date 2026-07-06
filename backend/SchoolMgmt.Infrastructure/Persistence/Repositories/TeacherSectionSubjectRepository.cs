using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.TeacherAssignments;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class TeacherSectionSubjectRepository(AppDbContext context)
    : Repository<TeacherSectionSubject>(context), ITeacherSectionSubjectRepository
{
    public async Task<List<TeacherSectionSubject>> GetByTeacherAndYearAsync(
        Guid teacherId, Guid academicYearId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(a => a.Subject)
            .Include(a => a.Section).ThenInclude(s => s.Grade)
            .Include(a => a.AcademicYear)
            .Where(a => a.TeacherId == teacherId && a.AcademicYearId == academicYearId)
            .OrderBy(a => a.Section.Grade.DisplayOrder)
            .ThenBy(a => a.Section.Name)
            .ThenBy(a => a.Subject.Name)
            .ToListAsync(ct);
    }

    public async Task<TeacherSectionSubject?> GetBySubjectSectionAndYearAsync(
        Guid subjectId, Guid sectionId, Guid academicYearId, CancellationToken ct = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(
                a => a.SubjectId == subjectId
                  && a.SectionId == sectionId
                  && a.AcademicYearId == academicYearId,
                ct);
    }
}
