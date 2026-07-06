using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Application.Enrollments.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/sections/{sectionId:guid}/enrollments")]
[Authorize(Roles = "Admin")]
public class SectionEnrollmentsController(EnrollmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBySectionAndYear(
        Guid sectionId, [FromQuery] Guid? academicYearId, CancellationToken ct)
    {
        if (academicYearId is null || academicYearId == Guid.Empty)
            return BadRequest("academicYearId query parameter is required.");
        var enrollments = await service.GetBySectionAndYearAsync(sectionId, academicYearId.Value, ct);
        return Ok(enrollments);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid sectionId, CreateEnrollmentRequest request, CancellationToken ct)
    {
        var enrollment = await service.CreateAsync(sectionId, request, ct);
        return Created($"/api/enrollments/{enrollment.Id}", enrollment);
    }
}
