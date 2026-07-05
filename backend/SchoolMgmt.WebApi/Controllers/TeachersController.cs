using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Teachers;
using SchoolMgmt.Application.Teachers.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/teachers")]
[Authorize(Roles = "Admin")]
public class TeachersController(TeacherService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateTeacherRequest request, CancellationToken ct)
    {
        var teacher = await service.CreateTeacherAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = teacher.Id }, teacher);
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
        var result = await service.GetTeachersAsync(isActive, search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var teacher = await service.GetTeacherByIdAsync(id, ct);
        return Ok(teacher);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTeacherRequest request, CancellationToken ct)
    {
        var teacher = await service.UpdateTeacherAsync(id, request, ct);
        return Ok(teacher);
    }
}
