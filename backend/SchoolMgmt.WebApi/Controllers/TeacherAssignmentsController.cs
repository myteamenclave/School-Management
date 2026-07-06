using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.TeacherAssignments;
using SchoolMgmt.Application.TeacherAssignments.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/teachers/{teacherId:guid}/assignments")]
[Authorize(Roles = "Admin")]
public class TeacherAssignmentsController(TeacherAssignmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByTeacherAndYear(
        Guid teacherId, [FromQuery] Guid? academicYearId, CancellationToken ct)
    {
        if (academicYearId is null || academicYearId == Guid.Empty)
            return BadRequest("academicYearId query parameter is required.");
        var assignments = await service.GetByTeacherAndYearAsync(teacherId, academicYearId.Value, ct);
        return Ok(assignments);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid teacherId, CreateTeacherAssignmentRequest request, CancellationToken ct)
    {
        var assignment = await service.CreateAsync(teacherId, request, ct);
        return CreatedAtAction(nameof(GetByTeacherAndYear),
            new { teacherId, academicYearId = assignment.AcademicYearId }, assignment);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid teacherId, Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(teacherId, id, ct);
        return NoContent();
    }
}
