using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Gradebook;
using SchoolMgmt.Application.Gradebook.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

// Route is "api/gradebook", NOT "api/grades" — the latter is owned by GradesController
// (grade-LEVELS, e.g. Grade 5). This controller is academic marks per subject/term.
[ApiController]
[Route("api/gradebook")]
[Authorize(Roles = "Admin,Teacher")]
public class GradebookController(GradebookService service) : ControllerBase
{
    // GET /api/gradebook/subject-roster?sectionId=&subjectId=&semesterId=  (Admin + Teacher)
    [HttpGet("subject-roster")]
    public async Task<IActionResult> GetSubjectRoster(
        [FromQuery] Guid sectionId,
        [FromQuery] Guid subjectId,
        [FromQuery] Guid semesterId,
        CancellationToken ct)
    {
        var result = await service.GetSubjectRosterAsync(sectionId, subjectId, semesterId, ct);
        return Ok(result);
    }

    // PUT /api/gradebook/bulk  (Teacher only; ownership + archive checks in service)
    [HttpPut("bulk")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> BulkUpsert(
        [FromBody] BulkUpsertGradesRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        var result = await service.BulkUpsertAsync(request, userId, ct);
        return Ok(result);
    }

    // GET /api/gradebook/student?studentId=&academicYearId=  (Admin + Teacher)
    [HttpGet("student")]
    public async Task<IActionResult> GetStudentGrades(
        [FromQuery] Guid studentId,
        [FromQuery] Guid academicYearId,
        CancellationToken ct)
    {
        var result = await service.GetStudentGradesAsync(studentId, academicYearId, ct);
        return Ok(result);
    }
}
