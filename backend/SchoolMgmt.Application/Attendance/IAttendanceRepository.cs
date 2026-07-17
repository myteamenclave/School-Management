using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Attendance;

public interface IAttendanceRepository : IRepository<AttendanceRecord>
{
    Task<List<AttendanceRecord>> GetBySectionAndDateAsync(Guid sectionId, DateOnly date, CancellationToken ct = default);
    Task<AttendanceRecord?> GetByStudentSectionAndDateAsync(Guid studentId, Guid sectionId, DateOnly date, CancellationToken ct = default);
    Task<List<AttendanceRecord>> GetByStudentAndYearAsync(Guid studentId, Guid academicYearId, CancellationToken ct = default);
}
