using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Gradebook;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class SubjectTermGradeRepository(AppDbContext context)
    : Repository<SubjectTermGrade>(context), ISubjectTermGradeRepository
{
    public Task<List<SubjectTermGrade>> GetBySubjectAndSemesterAsync(
        Guid subjectId, Guid semesterId, CancellationToken ct = default) =>
        DbSet
            .Where(g => g.SubjectId == subjectId && g.SemesterId == semesterId)
            .ToListAsync(ct);

    public Task<SubjectTermGrade?> GetByStudentSubjectSemesterAsync(
        Guid studentId, Guid subjectId, Guid semesterId, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(
            g => g.StudentId == studentId && g.SubjectId == subjectId && g.SemesterId == semesterId, ct);

    public Task<List<SubjectTermGrade>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        DbSet
            .Include(g => g.Subject)
            .Include(g => g.Semester)
            .Where(g => g.StudentId == studentId && g.AcademicYearId == academicYearId)
            .ToListAsync(ct);
}
