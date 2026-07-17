using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.AcademicYears.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/academic-years")]
[Authorize]
public class AcademicYearsController(AcademicYearService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var years = await service.GetAllAcademicYearsAsync(ct);
        return Ok(years);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var year = await service.GetAcademicYearByIdAsync(id, ct);
        return Ok(year);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateAcademicYearRequest request, CancellationToken ct)
    {
        var year = await service.CreateAcademicYearAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = year.Id }, year);
    }

    [HttpPut("{yearId:guid}/semesters/{semesterId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSemester(Guid yearId, Guid semesterId, UpdateSemesterRequest request, CancellationToken ct)
    {
        var semester = await service.UpdateSemesterAsync(semesterId, request, ct);
        return Ok(semester);
    }

    [HttpPost("{id:guid}/set-current")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetCurrent(Guid id, CancellationToken ct)
    {
        await service.SetCurrentYearAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{yearId:guid}/semesters/{semesterId:guid}/set-current")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetCurrentSemester(Guid yearId, Guid semesterId, CancellationToken ct)
    {
        await service.SetCurrentSemesterAsync(semesterId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await service.ArchiveAcademicYearAsync(id, ct);
        return NoContent();
    }
}
