using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Subjects;
using SchoolMgmt.Application.Subjects.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize(Roles = "Admin")]
public class SubjectsController(SubjectService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateSubjectRequest request, CancellationToken ct)
    {
        var subject = await service.CreateSubjectAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = subject.Id }, subject);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);
        var result = await service.GetSubjectsAsync(isActive, search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var subject = await service.GetSubjectByIdAsync(id, ct);
        return Ok(subject);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateSubjectRequest request, CancellationToken ct)
    {
        var subject = await service.UpdateSubjectAsync(id, request, ct);
        return Ok(subject);
    }
}
