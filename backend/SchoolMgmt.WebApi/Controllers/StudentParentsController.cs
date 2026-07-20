using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.ParentAccounts;
using SchoolMgmt.Application.ParentAccounts.Dtos;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/students/{studentId:guid}")]
[Authorize(Roles = "Admin")]
public class StudentParentsController(ParentAccountService service) : ControllerBase
{
    [HttpPost("parent-login")]
    public async Task<IActionResult> CreateParentLogin(Guid studentId, CreateParentLoginRequest request, CancellationToken ct)
    {
        var result = await service.CreateParentLoginAsync(studentId, request, ct);
        return Ok(result);
    }

    [HttpGet("parents")]
    public async Task<IActionResult> GetParents(Guid studentId, CancellationToken ct)
    {
        var parents = await service.GetParentsForStudentAsync(studentId, ct);
        return Ok(parents);
    }

    [HttpDelete("parents/{parentUserId:guid}")]
    public async Task<IActionResult> RemoveParentLink(Guid studentId, Guid parentUserId, CancellationToken ct)
    {
        await service.RemoveParentLinkAsync(studentId, parentUserId, ct);
        return NoContent();
    }
}
