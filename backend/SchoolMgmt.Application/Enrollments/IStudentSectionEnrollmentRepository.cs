using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Enrollments;

public interface IStudentSectionEnrollmentRepository : IRepository<StudentSectionEnrollment>
{
    Task<List<StudentSectionEnrollment>> GetBySectionAndYearAsync(
        Guid sectionId, Guid academicYearId, CancellationToken ct = default);

    Task<StudentSectionEnrollment?> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<StudentSectionEnrollment?> GetByIdWithDetailsAsync(
        Guid id, CancellationToken ct = default);

    Task<List<Guid>> GetEnrolledStudentIdsForYearAsync(
        Guid academicYearId, CancellationToken ct = default);

    Task<List<StudentSectionEnrollment>> GetByStudentIdAsync(
        Guid studentId, CancellationToken ct = default);

    Task<List<StudentSectionEnrollment>> GetByGradeAndYearAsync(
        Guid gradeId, Guid academicYearId, CancellationToken ct = default);
}
