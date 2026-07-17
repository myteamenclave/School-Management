using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Attendance;
using SchoolMgmt.Application.Attendance.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize(Roles = "Admin,Teacher")]
public class AttendanceController(AttendanceService service) : ControllerBase
{
    [HttpGet("section-roster")]
    public async Task<IActionResult> GetSectionRoster(
        [FromQuery] Guid sectionId,
        [FromQuery] DateOnly date,
        [FromQuery] Guid academicYearId,
        CancellationToken ct)
    {
        var result = await service.GetSectionRosterAsync(sectionId, date, academicYearId, ct);
        return Ok(result);
    }

    [HttpPut("bulk")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> BulkUpsert(
        [FromBody] BulkUpsertAttendanceRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        var result = await service.BulkUpsertAsync(request, userId, ct);
        return Ok(result);
    }

    [HttpGet("student-history")]
    public async Task<IActionResult> GetStudentHistory(
        [FromQuery] Guid studentId,
        [FromQuery] Guid academicYearId,
        CancellationToken ct)
    {
        var result = await service.GetStudentHistoryAsync(studentId, academicYearId, ct);
        return Ok(result);
    }
}
