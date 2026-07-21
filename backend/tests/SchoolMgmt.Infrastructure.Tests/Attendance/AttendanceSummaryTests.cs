using SchoolMgmt.Application.Attendance;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;
using SchoolMgmt.Infrastructure.Tests.Attendance.Fakes;

namespace SchoolMgmt.Infrastructure.Tests.Attendance;

// The attendance-rate formula is a business rule (spec 19): rate = (Present + Late) / TotalMarked,
// Absent AND Excused count against it, null at zero marked days. These isolate that math.
public class AttendanceSummaryTests
{
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid YearId = Guid.NewGuid();

    // AttendanceService uses only attendanceRepo for GetStudentSummaryAsync; the other
    // collaborators are never touched by this method, so null! is safe here.
    private static AttendanceService BuildService(FakeAttendanceRepository repo) =>
        new(repo, null!, null!, null!, null!, null!, null!);

    private static AttendanceRecord Record(AttendanceStatus status) => new()
    {
        StudentId = StudentId,
        AcademicYearId = YearId,
        SectionId = Guid.NewGuid(),
        Date = new DateOnly(2025, 9, 1),
        Status = status,
    };

    private static FakeAttendanceRepository RepoWith(params AttendanceStatus[] statuses)
    {
        var repo = new FakeAttendanceRepository();
        foreach (var s in statuses) repo.Seed(Record(s));
        return repo;
    }

    [Fact]
    public async Task MixedRecords_CountsAndRateCorrect()
    {
        var statuses =
            Enumerable.Repeat(AttendanceStatus.Present, 168)
                .Concat(Enumerable.Repeat(AttendanceStatus.Late, 4))
                .Concat(Enumerable.Repeat(AttendanceStatus.Absent, 6))
                .Concat(Enumerable.Repeat(AttendanceStatus.Excused, 2))
                .ToArray();
        var service = BuildService(RepoWith(statuses));

        var summary = await service.GetStudentSummaryAsync(StudentId, YearId);

        Assert.Equal(180, summary.TotalMarked);
        Assert.Equal(168, summary.PresentCount);
        Assert.Equal(4, summary.LateCount);
        Assert.Equal(6, summary.AbsentCount);
        Assert.Equal(2, summary.ExcusedCount);
        // (168 + 4) * 100 / 180 = 95.555… → 95.6
        Assert.Equal(95.6m, summary.AttendanceRate);
    }

    [Fact]
    public async Task ExcusedCountsAgainstTheRate()
    {
        // 8 Present, 2 Excused → Excused is NOT in the numerator: 8/10 = 80, not 100.
        var service = BuildService(RepoWith(
            AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Present,
            AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Present,
            AttendanceStatus.Excused, AttendanceStatus.Excused));

        var summary = await service.GetStudentSummaryAsync(StudentId, YearId);

        Assert.Equal(80m, summary.AttendanceRate);
        Assert.Equal(2, summary.ExcusedCount);
    }

    [Fact]
    public async Task LateCountsAsPresent()
    {
        // Present + Late only → everyone was physically in class → 100.
        var service = BuildService(RepoWith(
            AttendanceStatus.Present, AttendanceStatus.Late, AttendanceStatus.Late));

        var summary = await service.GetStudentSummaryAsync(StudentId, YearId);

        Assert.Equal(100m, summary.AttendanceRate);
    }

    [Fact]
    public async Task ZeroMarkedDays_RateIsNull()
    {
        var service = BuildService(new FakeAttendanceRepository());

        var summary = await service.GetStudentSummaryAsync(StudentId, YearId);

        Assert.Equal(0, summary.TotalMarked);
        Assert.Null(summary.AttendanceRate);
    }
}
