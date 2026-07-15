using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.FeeInvoices;

public interface IStudentFeeAssignmentRepository : IRepository<StudentFeeAssignment>
{
    Task<StudentFeeAssignment?> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<List<StudentFeeAssignment>> GetByTemplateAsync(
        Guid templateId, CancellationToken ct = default);

    Task<List<StudentFeeAssignment>> GetByGradeAndYearAsync(
        Guid gradeId, Guid academicYearId, CancellationToken ct = default);
}
