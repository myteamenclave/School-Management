using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Attendance;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Application.FeeInvoices.Dtos;
using SchoolMgmt.Application.Gradebook;
using SchoolMgmt.Application.Gradebook.Dtos;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.ParentAccounts;
using SchoolMgmt.Application.ParentPortal.Dtos;
using SchoolMgmt.Application.Payments;
using SchoolMgmt.Application.Payments.Dtos;
using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Application.ParentPortal;

// First parent-facing read surface, and (via the pay/confirm methods) the first parent write path.
// Every child-scoped call passes through ResolveLinkedChildOrThrow — no endpoint trusts a
// client-supplied student id.
public class ParentPortalService(
    IStudentParentRepository links,
    IStudentSectionEnrollmentRepository enrollments,
    IAcademicYearRepository years,
    GradebookService gradebook,
    AttendanceService attendance,
    FeeInvoiceService fees,
    PaymentService payments,
    ITenantProvider tenantProvider)
{
    // The caller's linked children, labelled with their current-year grade/section.
    public async Task<List<ParentChildDto>> GetMyChildrenAsync(Guid parentUserId, CancellationToken ct = default)
    {
        var childLinks = await links.GetByUserIdAsync(parentUserId, ct);
        var currentYear = await years.GetCurrentAsync(ct);

        var result = new List<ParentChildDto>(childLinks.Count);
        foreach (var link in childLinks)
        {
            var s = link.Student;
            string? gradeLabel = null, sectionName = null;
            if (currentYear is not null)
            {
                var enrollment = (await enrollments.GetByStudentIdAsync(s.Id, ct))
                    .FirstOrDefault(e => e.AcademicYearId == currentYear.Id);
                if (enrollment is not null)
                {
                    gradeLabel = enrollment.Section.Grade.Name;
                    sectionName = enrollment.Section.Name;
                }
            }

            result.Add(new ParentChildDto(
                s.Id,
                $"{s.FirstName} {s.LastName}",
                s.StudentCode,
                s.EnrollmentStatus.ToString(),
                gradeLabel,
                sectionName));
        }

        return result;
    }

    // Grades for one linked child. academicYearId null => current year.
    public async Task<List<StudentGradeDto>> GetChildGradesAsync(
        Guid parentUserId, Guid childId, Guid? academicYearId, CancellationToken ct = default)
    {
        await ResolveLinkedChildOrThrow(parentUserId, childId, ct);

        var yearId = academicYearId
            ?? (await years.GetCurrentAsync(ct))?.Id
            ?? throw new NotFoundException("No current academic year is set.");

        return await gradebook.GetStudentGradesAsync(childId, yearId, ct);
    }

    // Attendance (summary + daily log) for one linked child. academicYearId null => current year.
    public async Task<ParentAttendanceDto> GetChildAttendanceAsync(
        Guid parentUserId, Guid childId, Guid? academicYearId, CancellationToken ct = default)
    {
        await ResolveLinkedChildOrThrow(parentUserId, childId, ct);

        var yearId = academicYearId
            ?? (await years.GetCurrentAsync(ct))?.Id
            ?? throw new NotFoundException("No current academic year is set.");

        var summary = await attendance.GetStudentSummaryAsync(childId, yearId, ct);
        var entries = await attendance.GetStudentHistoryAsync(childId, yearId, ct);
        return new ParentAttendanceDto(summary, entries);
    }

    // Fee balance + Issued invoice for one linked child. academicYearId null => current year.
    public async Task<StudentFeeOverviewDto> GetChildFeesAsync(
        Guid parentUserId, Guid childId, Guid? academicYearId, CancellationToken ct = default)
    {
        await ResolveLinkedChildOrThrow(parentUserId, childId, ct);

        var yearId = academicYearId
            ?? (await years.GetCurrentAsync(ct))?.Id
            ?? throw new NotFoundException("No current academic year is set.");

        return await fees.GetStudentFeeOverviewAsync(childId, yearId, ct);
    }

    // Start paying one installment of a linked child's invoice. Guard proves the child is the
    // caller's; PaymentService proves the installment is that child's and re-derives the amount.
    public async Task<InitiatePaymentResult> PayChildInstallmentAsync(
        Guid parentUserId, Guid childId, Guid installmentId, CancellationToken ct = default)
    {
        await ResolveLinkedChildOrThrow(parentUserId, childId, ct);
        return await payments.InitiateInstallmentPaymentAsync(installmentId, childId, ct);
    }

    // Confirm after Stripe.js resolves — verified with Stripe server-side, then reconciled.
    public async Task ConfirmChildPaymentAsync(
        Guid parentUserId, Guid childId, Guid paymentId, CancellationToken ct = default)
    {
        await ResolveLinkedChildOrThrow(parentUserId, childId, ct);
        await payments.ConfirmPaymentAsync(paymentId, tenantProvider.CurrentSchoolId, childId, ct);
    }

    // Minimal year list for the selector.
    public async Task<List<ParentAcademicYearDto>> GetAcademicYearsAsync(CancellationToken ct = default) =>
        (await years.GetAllWithSemestersAsync(ct))
            .OrderByDescending(y => y.StartDate)
            .Select(y => new ParentAcademicYearDto(y.Id, y.Name, y.IsCurrent))
            .ToList();

    // The single authorization surface. Unlinked OR unknown child => 404 (never leak existence).
    private async Task ResolveLinkedChildOrThrow(Guid parentUserId, Guid childId, CancellationToken ct)
    {
        var link = await links.GetLinkAsync(childId, parentUserId, ct);
        if (link is null)
            throw new NotFoundException("Child not found.");
    }
}
