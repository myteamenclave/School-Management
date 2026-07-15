using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.FeeInvoices;

public interface IStudentDiscountAssignmentRepository : IRepository<StudentDiscountAssignment>
{
    Task<List<StudentDiscountAssignment>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<StudentDiscountAssignment?> GetByStudentRuleAndYearAsync(
        Guid studentId, Guid discountRuleId, Guid academicYearId, CancellationToken ct = default);
}
