using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Students;
using SchoolMgmt.Application.Students.Dtos;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Roles = "Admin")]
public class StudentsController(StudentService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateStudentRequest request, CancellationToken ct)
    {
        var student = await service.CreateStudentAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = student.Id }, student);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        EnrollmentStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<EnrollmentStatus>(status, ignoreCase: true, out var s))
            parsedStatus = s;

        pageSize = Math.Min(pageSize, 100);
        var result = await service.GetStudentsAsync(parsedStatus, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var student = await service.GetStudentByIdAsync(id, ct);
        return Ok(student);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateStudentRequest request, CancellationToken ct)
    {
        var student = await service.UpdateStudentAsync(id, request, ct);
        return Ok(student);
    }
}
