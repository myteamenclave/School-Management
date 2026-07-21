using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.ParentAccounts;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class StudentParentRepository(AppDbContext context)
    : Repository<StudentParent>(context), IStudentParentRepository
{
    public Task<StudentParent?> GetLinkAsync(Guid studentId, Guid userId, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(x => x.StudentId == studentId && x.UserId == userId, ct);

    public Task<List<StudentParent>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default) =>
        DbSet
            .Include(x => x.ParentUser)
            .Where(x => x.StudentId == studentId)
            .OrderBy(x => x.ParentUser.DisplayName)
            .ToListAsync(ct);

    public Task<List<StudentParent>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        DbSet
            .Include(x => x.Student)
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Student.LastName)
            .ThenBy(x => x.Student.FirstName)
            .ToListAsync(ct);
}
