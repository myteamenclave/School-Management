using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Dashboard.Dtos;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Application.Dashboard;

public class DashboardService(
    IAcademicYearRepository yearRepo,
    IDashboardRepository dashboard,
    IDateTimeProvider clock)
{
    public async Task<DashboardOverviewDto> GetOverviewAsync(Guid? academicYearId, CancellationToken ct = default)
    {
        var year = academicYearId is Guid id
            ? await yearRepo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException("Academic year not found.")
            : await yearRepo.GetCurrentAsync(ct)
                ?? throw new NotFoundException("No current academic year is set.");

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var finance = await dashboard.GetFinanceSummaryAsync(year.Id, today, ct);
        var financeMonthly = await dashboard.GetMonthlyFinanceAsync(year.Id, year.StartDate, year.EndDate, ct);
        var attendanceMonthly = await dashboard.GetMonthlyAttendanceAsync(year.Id, year.StartDate, year.EndDate, ct);
        var enrollment = await dashboard.GetEnrollmentBreakdownAsync(year.Id, ct);
        var teachers = await dashboard.GetTeacherCoverageAsync(year.Id, ct);

        return new DashboardOverviewDto(
            year.Id, year.Name, finance, financeMonthly, attendanceMonthly, enrollment, teachers);
    }
}
