using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.ParentAccounts;

public interface IStudentParentRepository : IRepository<StudentParent>
{
    Task<StudentParent?> GetLinkAsync(Guid studentId, Guid userId, CancellationToken ct = default);

    Task<List<StudentParent>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default);

    // Parent → children direction (parent portal). Includes Student.
    Task<List<StudentParent>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
