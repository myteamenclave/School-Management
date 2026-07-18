using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Gradebook;

public interface ISubjectTermGradeRepository : IRepository<SubjectTermGrade>
{
    // Grades for a subject in a semester.
    Task<List<SubjectTermGrade>> GetBySubjectAndSemesterAsync(
        Guid subjectId, Guid semesterId, CancellationToken ct = default);

    // The single grade for a student+subject+semester (identity lookup for upsert).
    Task<SubjectTermGrade?> GetByStudentSubjectSemesterAsync(
        Guid studentId, Guid subjectId, Guid semesterId, CancellationToken ct = default);

    // A student's grades across subjects for a year (parent portal / dashboard / student detail).
    Task<List<SubjectTermGrade>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);
}
