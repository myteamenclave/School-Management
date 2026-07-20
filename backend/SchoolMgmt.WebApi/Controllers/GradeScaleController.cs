using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Gradebook;
using SchoolMgmt.Application.Gradebook.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/grade-scale")]
[Authorize(Roles = "Admin,Teacher")]
public class GradeScaleController(GradeScaleService service) : ControllerBase
{
    // Read is Admin + Teacher — the teacher gradebook needs the bands to map
    // term scores to letters live. Writes below remain Admin-only.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] UpsertGradeScaleBandRequest req, CancellationToken ct)
        => Ok(await service.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertGradeScaleBandRequest req, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
