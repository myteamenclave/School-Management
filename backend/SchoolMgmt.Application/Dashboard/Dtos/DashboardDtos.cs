namespace SchoolMgmt.Application.Dashboard.Dtos;

// Root payload for GET /api/dashboard/overview — a read model, scoped to one academic year.
public record DashboardOverviewDto(
    Guid AcademicYearId,
    string AcademicYearName,
    FinanceSummaryDto Finance,
    IReadOnlyList<MonthlyMoneyPointDto> FinanceMonthly,
    IReadOnlyList<MonthlyAttendancePointDto> AttendanceMonthly,
    EnrollmentBreakdownDto Enrollment,
    TeacherCoverageDto Teachers);

// Finance — only ISSUED invoices count as billed obligations (Draft = not yet real, Cancelled = excluded).
// Overdue is COMPUTED from due dates, not read from InstallmentStatus.Overdue (no job keeps that current).
public record FinanceSummaryDto(
    decimal Billed,
    decimal Collected,
    decimal Outstanding,
    decimal Overdue,
    decimal CollectionRate,   // Collected / Billed, 0 when Billed = 0
    int IssuedInvoiceCount,
    int DraftInvoiceCount);   // exception signal: billed value not yet issued

// Billed keyed on installment DueDate month; Collected keyed on PaidAt month. Issued invoices only.
public record MonthlyMoneyPointDto(int Year, int Month, decimal Billed, decimal Collected);

// PresentCount = Present + Late; PresentRate = PresentCount / TotalRecords (0 when Total = 0).
public record MonthlyAttendancePointDto(int Year, int Month, int TotalRecords, int PresentCount, double PresentRate);

public record EnrollmentBreakdownDto(
    int TotalEnrolled,                          // distinct students with a section enrollment this year
    IReadOnlyList<GradeCountDto> ByGrade,
    IReadOnlyList<StatusCountDto> ByStatus);    // Student.EnrollmentStatus of enrolled students

public record GradeCountDto(Guid GradeId, string GradeName, int Count);

public record StatusCountDto(string Status, int Count);

// Only coverage the schema can honestly compute (there is no section-subject curriculum map).
public record TeacherCoverageDto(
    int TeacherCount,                 // active teachers
    int AssignmentCount,              // TeacherSectionSubject rows for the year
    int SectionsWithEnrollments,      // sections with >=1 student enrolled this year
    int SectionsWithoutAnyTeacher,    // GAP: section has students but zero teacher assignments
    int TeachersWithoutAssignment);   // GAP: active teacher with zero assignments this year
