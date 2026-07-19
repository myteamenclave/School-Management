using SchoolMgmt.Application.Dashboard.Dtos;

namespace SchoolMgmt.Application.Dashboard;

// Read-model repository: read-only aggregations spanning several DbSets for the admin overview.
// Deliberately not one-repo-per-entity (see spec 16 A5) — it serves a query model, never persists
// and never transacts. Every method returns pre-aggregated rows, not entity graphs.
public interface IDashboardRepository
{
    Task<FinanceSummaryDto> GetFinanceSummaryAsync(Guid academicYearId, DateOnly today, CancellationToken ct = default);

    Task<List<MonthlyMoneyPointDto>> GetMonthlyFinanceAsync(
        Guid academicYearId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    Task<List<MonthlyAttendancePointDto>> GetMonthlyAttendanceAsync(
        Guid academicYearId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    Task<EnrollmentBreakdownDto> GetEnrollmentBreakdownAsync(Guid academicYearId, CancellationToken ct = default);

    Task<TeacherCoverageDto> GetTeacherCoverageAsync(Guid academicYearId, CancellationToken ct = default);
}
