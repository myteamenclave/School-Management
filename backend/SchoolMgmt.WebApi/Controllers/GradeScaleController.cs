using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Gradebook;
using SchoolMgmt.Application.Gradebook.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/grade-scale")]
[Authorize(Roles = "Admin")]
public class GradeScaleController(GradeScaleService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertGradeScaleBandRequest req, CancellationToken ct)
        => Ok(await service.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertGradeScaleBandRequest req, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
