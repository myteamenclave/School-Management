using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Application.FeeInvoices.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/fee-assignments")]
[Authorize(Roles = "Admin")]
public class FeeAssignmentsController(FeeInvoiceService service) : ControllerBase
{
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest request, CancellationToken ct)
    {
        var result = await service.BroadcastAssignmentAsync(request.TemplateId, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignment(
        [FromQuery] Guid studentId, [FromQuery] Guid academicYearId, CancellationToken ct)
    {
        var result = await service.GetStudentAssignmentAsync(studentId, academicYearId, ct);
        return Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> SetAssignment(
        [FromQuery] Guid studentId, [FromBody] SetStudentAssignmentRequest request, CancellationToken ct)
    {
        var result = await service.SetStudentAssignmentAsync(studentId, request, ct);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<IActionResult> RemoveAssignment(
        [FromQuery] Guid studentId, [FromQuery] Guid academicYearId, CancellationToken ct)
    {
        await service.RemoveStudentAssignmentAsync(studentId, academicYearId, ct);
        return NoContent();
    }

    [HttpGet("discounts")]
    public async Task<IActionResult> GetDiscounts(
        [FromQuery] Guid studentId, [FromQuery] Guid academicYearId, CancellationToken ct)
    {
        var result = await service.GetStudentDiscountsAsync(studentId, academicYearId, ct);
        return Ok(result);
    }

    [HttpPost("discounts")]
    public async Task<IActionResult> AddDiscount(
        [FromQuery] Guid studentId, [FromBody] AddStudentDiscountRequest request, CancellationToken ct)
    {
        var result = await service.AddStudentDiscountAsync(studentId, request, ct);
        return Ok(result);
    }

    [HttpDelete("discounts/{id:guid}")]
    public async Task<IActionResult> RemoveDiscount(Guid id, CancellationToken ct)
    {
        await service.RemoveStudentDiscountAsync(id, ct);
        return NoContent();
    }
}
