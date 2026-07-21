using SchoolMgmt.Application.Attendance;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Tests.Attendance.Fakes;

// Minimal in-memory IAttendanceRepository. Only GetByStudentAndYearAsync carries behaviour;
// the summary math is what we're isolating.
public class FakeAttendanceRepository : IAttendanceRepository
{
    public List<AttendanceRecord> Records { get; } = new();

    public void Seed(AttendanceRecord record) => Records.Add(record);

    public Task<List<AttendanceRecord>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        Task.FromResult(Records
            .Where(r => r.StudentId == studentId && r.AcademicYearId == academicYearId)
            .ToList());

    public Task<List<AttendanceRecord>> GetBySectionAndDateAsync(
        Guid sectionId, DateOnly date, CancellationToken ct = default) =>
        Task.FromResult(Records.Where(r => r.SectionId == sectionId && r.Date == date).ToList());

    public Task<AttendanceRecord?> GetByStudentSectionAndDateAsync(
        Guid studentId, Guid sectionId, DateOnly date, CancellationToken ct = default) =>
        Task.FromResult(Records.FirstOrDefault(r =>
            r.StudentId == studentId && r.SectionId == sectionId && r.Date == date));

    public Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records.FirstOrDefault(r => r.Id == id));

    public Task AddAsync(AttendanceRecord entity, CancellationToken cancellationToken = default)
    {
        Records.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(AttendanceRecord entity) { }

    public void Remove(AttendanceRecord entity) => Records.RemoveAll(r => r.Id == entity.Id);
}
