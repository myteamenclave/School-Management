using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Application.Enrollments.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize(Roles = "Admin")]
public class EnrollmentsController(EnrollmentService service) : ControllerBase
{
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Transfer(
        Guid id, TransferEnrollmentRequest request, CancellationToken ct)
    {
        var enrollment = await service.TransferAsync(id, request, ct);
        return Ok(enrollment);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
