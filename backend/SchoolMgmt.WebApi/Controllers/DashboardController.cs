using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Dashboard;

namespace SchoolMgmt.WebApi.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Admin")]
public class DashboardController(DashboardService service) : ControllerBase
{
    // Read-only aggregation. academicYearId optional — defaults to the current year.
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] Guid? academicYearId, CancellationToken ct)
        => Ok(await service.GetOverviewAsync(academicYearId, ct));
}
