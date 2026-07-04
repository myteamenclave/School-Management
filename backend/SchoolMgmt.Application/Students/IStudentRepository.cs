using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.Students;

public interface IStudentRepository : IRepository<Student>
{
    Task<(List<Student> Items, int TotalCount)> GetPagedAsync(
        EnrollmentStatus? status, string? search, int page, int pageSize, CancellationToken ct = default);

    Task<string> GetNextStudentCodeAsync(int enrollmentYear, CancellationToken ct = default);
}
