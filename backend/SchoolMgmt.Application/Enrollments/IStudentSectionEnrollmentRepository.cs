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
}
