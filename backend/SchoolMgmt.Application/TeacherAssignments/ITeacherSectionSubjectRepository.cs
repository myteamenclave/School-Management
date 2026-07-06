using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.TeacherAssignments;

public interface ITeacherSectionSubjectRepository : IRepository<TeacherSectionSubject>
{
    Task<List<TeacherSectionSubject>> GetByTeacherAndYearAsync(
        Guid teacherId, Guid academicYearId, CancellationToken ct = default);

    Task<TeacherSectionSubject?> GetBySubjectSectionAndYearAsync(
        Guid subjectId, Guid sectionId, Guid academicYearId, CancellationToken ct = default);
}
