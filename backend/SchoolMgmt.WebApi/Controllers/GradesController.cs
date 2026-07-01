using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Grades.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/grades")]
[Authorize(Roles = "Admin")]
public class GradesController(GradeService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var grades = await service.GetAllGradesAsync(ct);
        return Ok(grades);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var grade = await service.GetGradeByIdAsync(id, ct);
        return Ok(grade);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateGradeRequest request, CancellationToken ct)
    {
        var grade = await service.CreateGradeAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = grade.Id }, grade);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateGradeRequest request, CancellationToken ct)
    {
        var grade = await service.UpdateGradeAsync(id, request, ct);
        return Ok(grade);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteGradeAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{gradeId:guid}/sections")]
    public async Task<IActionResult> AddSection(Guid gradeId, CreateSectionRequest request, CancellationToken ct)
    {
        var section = await service.AddSectionAsync(gradeId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = gradeId }, section);
    }

    [HttpPut("{gradeId:guid}/sections/{sectionId:guid}")]
    public async Task<IActionResult> UpdateSection(Guid gradeId, Guid sectionId, UpdateSectionRequest request, CancellationToken ct)
    {
        var section = await service.UpdateSectionAsync(gradeId, sectionId, request, ct);
        return Ok(section);
    }

    [HttpDelete("{gradeId:guid}/sections/{sectionId:guid}")]
    public async Task<IActionResult> DeleteSection(Guid gradeId, Guid sectionId, CancellationToken ct)
    {
        await service.DeleteSectionAsync(gradeId, sectionId, ct);
        return NoContent();
    }
}
