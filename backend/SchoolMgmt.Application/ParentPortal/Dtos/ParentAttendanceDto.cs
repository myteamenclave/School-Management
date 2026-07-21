using SchoolMgmt.Application.Attendance.Dtos;

namespace SchoolMgmt.Application.ParentPortal.Dtos;

// Combined payload for the parent attendance view: the year summary (hero) plus the
// full reverse-chronological daily log (reusing the spec-14 history row DTO unchanged).
public record ParentAttendanceDto(
    StudentAttendanceSummaryDto Summary,
    List<AttendanceHistoryEntryDto> Entries
);
