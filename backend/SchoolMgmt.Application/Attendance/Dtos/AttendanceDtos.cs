namespace SchoolMgmt.Application.Attendance.Dtos;

public record AttendanceRosterEntryDto(
    Guid StudentId,
    string StudentName,
    string StudentCode,
    string? Status,
    string? Notes
);

public record SectionAttendanceRosterDto(
    Guid SectionId,
    string SectionName,
    DateOnly Date,
    List<AttendanceRosterEntryDto> Entries
);

public record BulkUpsertAttendanceRequest(
    Guid SectionId,
    Guid AcademicYearId,
    DateOnly Date,
    List<AttendanceEntryRequest> Entries
);

public record AttendanceEntryRequest(
    Guid StudentId,
    string Status,
    string? Notes
);

public record BulkUpsertAttendanceResult(int Upserted);

public record AttendanceHistoryEntryDto(
    Guid Id,
    Guid SectionId,
    string SectionName,
    DateOnly Date,
    string Status,
    string? Notes
);

// Per-year attendance summary for a single student. Rate is (Present + Late) / TotalMarked,
// expressed 0–100 (one decimal); null when TotalMarked == 0 (no days marked — never divide-by-zero).
public record StudentAttendanceSummaryDto(
    int TotalMarked,
    int PresentCount,
    int LateCount,
    int AbsentCount,
    int ExcusedCount,
    decimal? AttendanceRate
);
