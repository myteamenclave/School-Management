using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Dashboard;
using SchoolMgmt.Application.Dashboard.Dtos;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

// Read-only aggregation across several DbSets (see spec 16 A5). Never persists, never transacts.
// Tenant scoping is applied automatically by AppDbContext global query filters — do NOT filter SchoolId here.
internal sealed class DashboardRepository(AppDbContext context) : IDashboardRepository
{
    public async Task<FinanceSummaryDto> GetFinanceSummaryAsync(
        Guid academicYearId, DateOnly today, CancellationToken ct = default)
    {
        var issued = context.FeeInvoiceInstallments
            .Where(i => i.FeeInvoice.AcademicYearId == academicYearId
                     && i.FeeInvoice.Status == InvoiceStatus.Issued);

        var billed = await issued.SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        var collected = await issued.SumAsync(i => i.AmountPaid, ct) ?? 0m;

        var overdue = await issued
            .Where(i => i.DueDate != null && i.DueDate < today && (i.AmountPaid ?? 0m) < i.Amount)
            .SumAsync(i => (decimal?)(i.Amount - (i.AmountPaid ?? 0m)), ct) ?? 0m;

        var issuedCount = await context.FeeInvoices
            .CountAsync(inv => inv.AcademicYearId == academicYearId && inv.Status == InvoiceStatus.Issued, ct);
        var draftCount = await context.FeeInvoices
            .CountAsync(inv => inv.AcademicYearId == academicYearId && inv.Status == InvoiceStatus.Draft, ct);

        var outstanding = billed - collected;
        var collectionRate = billed == 0m ? 0m : Math.Round(collected / billed, 4);

        return new FinanceSummaryDto(
            billed, collected, outstanding, overdue, collectionRate, issuedCount, draftCount);
    }

    public async Task<List<MonthlyMoneyPointDto>> GetMonthlyFinanceAsync(
        Guid academicYearId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var issued = context.FeeInvoiceInstallments
            .Where(i => i.FeeInvoice.AcademicYearId == academicYearId
                     && i.FeeInvoice.Status == InvoiceStatus.Issued);

        var billedByMonth = await issued
            .Where(i => i.DueDate != null)
            .GroupBy(i => new { i.DueDate!.Value.Year, i.DueDate!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var collectedByMonth = await issued
            .Where(i => i.PaidAt != null)
            .GroupBy(i => new { i.PaidAt!.Value.Year, i.PaidAt!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.AmountPaid ?? 0m) })
            .ToListAsync(ct);

        var billedDict = billedByMonth.ToDictionary(x => (x.Year, x.Month), x => x.Amount);
        var collectedDict = collectedByMonth.ToDictionary(x => (x.Year, x.Month), x => x.Amount);

        return MonthSequence(startDate, endDate)
            .Select(k => new MonthlyMoneyPointDto(
                k.Year, k.Month,
                billedDict.GetValueOrDefault(k),
                collectedDict.GetValueOrDefault(k)))
            .ToList();
    }

    public async Task<List<MonthlyAttendancePointDto>> GetMonthlyAttendanceAsync(
        Guid academicYearId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var byMonth = await context.AttendanceRecords
            .Where(a => a.AcademicYearId == academicYearId)
            .GroupBy(a => new { a.Date.Year, a.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late),
            })
            .ToListAsync(ct);

        var dict = byMonth.ToDictionary(x => (x.Year, x.Month));

        return MonthSequence(startDate, endDate)
            .Select(k =>
            {
                dict.TryGetValue(k, out var v);
                var total = v?.Total ?? 0;
                var present = v?.Present ?? 0;
                var rate = total == 0 ? 0d : Math.Round((double)present / total, 4);
                return new MonthlyAttendancePointDto(k.Year, k.Month, total, present, rate);
            })
            .ToList();
    }

    public async Task<EnrollmentBreakdownDto> GetEnrollmentBreakdownAsync(
        Guid academicYearId, CancellationToken ct = default)
    {
        var enrollments = context.StudentSectionEnrollments
            .Where(e => e.AcademicYearId == academicYearId);

        var totalEnrolled = await enrollments.Select(e => e.StudentId).Distinct().CountAsync(ct);

        var byGrade = await enrollments
            .GroupBy(e => new { e.Section.GradeId, e.Section.Grade.Name, e.Section.Grade.DisplayOrder })
            .OrderBy(g => g.Key.DisplayOrder)
            .Select(g => new GradeCountDto(g.Key.GradeId, g.Key.Name, g.Count()))
            .ToListAsync(ct);

        var byStatusRaw = await enrollments
            .Select(e => new { e.StudentId, e.Student.EnrollmentStatus })
            .Distinct()
            .GroupBy(x => x.EnrollmentStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = byStatusRaw
            .Select(x => new StatusCountDto(x.Status.ToString(), x.Count))
            .ToList();

        return new EnrollmentBreakdownDto(totalEnrolled, byGrade, byStatus);
    }

    public async Task<TeacherCoverageDto> GetTeacherCoverageAsync(
        Guid academicYearId, CancellationToken ct = default)
    {
        var teacherCount = await context.Teachers.CountAsync(t => t.IsActive, ct);

        var assignments = context.TeacherSectionSubjects.Where(a => a.AcademicYearId == academicYearId);
        var assignmentCount = await assignments.CountAsync(ct);
        var sectionsWithTeacher = await assignments.Select(a => a.SectionId).Distinct().ToListAsync(ct);
        var teachersWithAssignment = await assignments.Select(a => a.TeacherId).Distinct().ToListAsync(ct);

        var sectionsWithEnrollmentIds = await context.StudentSectionEnrollments
            .Where(e => e.AcademicYearId == academicYearId)
            .Select(e => e.SectionId).Distinct().ToListAsync(ct);

        var activeTeacherIds = await context.Teachers
            .Where(t => t.IsActive).Select(t => t.Id).ToListAsync(ct);

        var sectionsWithoutAnyTeacher = sectionsWithEnrollmentIds.Except(sectionsWithTeacher).Count();
        var teachersWithoutAssignment = activeTeacherIds.Except(teachersWithAssignment).Count();

        return new TeacherCoverageDto(
            teacherCount,
            assignmentCount,
            sectionsWithEnrollmentIds.Count,
            sectionsWithoutAnyTeacher,
            teachersWithoutAssignment);
    }

    // Inclusive month sequence spanning the academic year, so months with no data render as zero points.
    private static IEnumerable<(int Year, int Month)> MonthSequence(DateOnly start, DateOnly end)
    {
        var year = start.Year;
        var month = start.Month;
        while (year < end.Year || (year == end.Year && month <= end.Month))
        {
            yield return (year, month);
            month++;
            if (month > 12) { month = 1; year++; }
        }
    }
}
