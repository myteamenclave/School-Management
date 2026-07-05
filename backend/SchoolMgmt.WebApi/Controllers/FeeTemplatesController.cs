using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.FeeTemplates;
using SchoolMgmt.Application.FeeTemplates.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/fee-templates")]
[Authorize(Roles = "Admin")]
public class FeeTemplatesController(FeeTemplateService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateFeeTemplateRequest request, CancellationToken ct)
    {
        var template = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? gradeId,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);
        var result = await service.GetTemplatesAsync(academicYearId, gradeId, isActive, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var template = await service.GetByIdAsync(id, ct);
        return Ok(template);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateHeader(Guid id, UpdateFeeTemplateRequest request, CancellationToken ct)
    {
        var template = await service.UpdateHeaderAsync(id, request, ct);
        return Ok(template);
    }

    [HttpPut("{id:guid}/line-items")]
    public async Task<IActionResult> ReplaceLineItems(Guid id, ReplaceLineItemsRequest request, CancellationToken ct)
    {
        var template = await service.ReplaceLineItemsAsync(id, request, ct);
        return Ok(template);
    }

    [HttpPut("{id:guid}/installments")]
    public async Task<IActionResult> ReplaceInstallments(Guid id, ReplaceInstallmentsRequest request, CancellationToken ct)
    {
        var template = await service.ReplaceInstallmentsAsync(id, request, ct);
        return Ok(template);
    }

    [HttpPut("{id:guid}/discount-rules")]
    public async Task<IActionResult> ReplaceDiscountRules(Guid id, ReplaceDiscountRulesRequest request, CancellationToken ct)
    {
        var template = await service.ReplaceDiscountRulesAsync(id, request, ct);
        return Ok(template);
    }
}
