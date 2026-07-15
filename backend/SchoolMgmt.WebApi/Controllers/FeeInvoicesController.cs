using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Application.FeeInvoices.Dtos;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/fee-invoices")]
[Authorize(Roles = "Admin")]
public class FeeInvoicesController(FeeInvoiceService service) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateInvoicesRequest request, CancellationToken ct)
    {
        var result = await service.GenerateInvoicesAsync(request, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] Guid? gradeId,
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);
        var result = await service.GetInvoicesAsync(status, gradeId, academicYearId, studentId, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await service.GetInvoiceByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<IActionResult> Issue(Guid id, CancellationToken ct)
    {
        var result = await service.IssueInvoiceAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("bulk-issue")]
    public async Task<IActionResult> BulkIssue([FromBody] BulkIssueRequest request, CancellationToken ct)
    {
        var result = await service.BulkIssueAsync(request.Ids, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await service.CancelInvoiceAsync(id, ct);
        return Ok(result);
    }
}
