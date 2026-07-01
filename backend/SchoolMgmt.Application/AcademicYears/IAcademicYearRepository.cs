using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.AcademicYears;

public interface IAcademicYearRepository : IRepository<AcademicYear>
{
    Task<List<AcademicYear>> GetAllWithSemestersAsync(CancellationToken ct = default);
    Task<AcademicYear?> GetWithSemestersAsync(Guid id, CancellationToken ct = default);
    Task<AcademicYear?> GetCurrentAsync(CancellationToken ct = default);
    Task<Semester?> GetCurrentSemesterAsync(CancellationToken ct = default);
    Task<Semester?> GetSemesterByIdAsync(Guid semesterId, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
}
