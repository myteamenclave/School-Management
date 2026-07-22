using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.ParentPortal;

namespace SchoolMgmt.WebApi.Controllers;

// First parent-facing read surface. Parent-only, all GET (read-only per the SameSite=Lax
// CSRF rule). The caller identity is the JWT sub; child id is validated against the caller's
// StudentParent links inside the service.
[ApiController]
[Route("api/parent")]
[Authorize(Roles = "Parent")]
public class ParentPortalController(ParentPortalService service) : ControllerBase
{
    private Guid ParentUserId => Guid.Parse(User.FindFirstValue("sub")!);

    // GET /api/parent/children
    [HttpGet("children")]
    public async Task<IActionResult> GetChildren(CancellationToken ct)
        => Ok(await service.GetMyChildrenAsync(ParentUserId, ct));

    // GET /api/parent/children/{childId}/grades?academicYearId=
    [HttpGet("children/{childId:guid}/grades")]
    public async Task<IActionResult> GetChildGrades(
        Guid childId, [FromQuery] Guid? academicYearId, CancellationToken ct)
        => Ok(await service.GetChildGradesAsync(ParentUserId, childId, academicYearId, ct));

    // GET /api/parent/children/{childId}/attendance?academicYearId=
    [HttpGet("children/{childId:guid}/attendance")]
    public async Task<IActionResult> GetChildAttendance(
        Guid childId, [FromQuery] Guid? academicYearId, CancellationToken ct)
        => Ok(await service.GetChildAttendanceAsync(ParentUserId, childId, academicYearId, ct));

    // GET /api/parent/children/{childId}/fees?academicYearId=
    [HttpGet("children/{childId:guid}/fees")]
    public async Task<IActionResult> GetChildFees(
        Guid childId, [FromQuery] Guid? academicYearId, CancellationToken ct)
        => Ok(await service.GetChildFeesAsync(ParentUserId, childId, academicYearId, ct));

    // GET /api/parent/academic-years
    [HttpGet("academic-years")]
    public async Task<IActionResult> GetAcademicYears(CancellationToken ct)
        => Ok(await service.GetAcademicYearsAsync(ct));

    // POST /api/parent/children/{childId}/installments/{installmentId}/pay
    // First parent write path — starts an online payment (POST per SameSite=Lax CSRF rule).
    [HttpPost("children/{childId:guid}/installments/{installmentId:guid}/pay")]
    public async Task<IActionResult> PayInstallment(
        Guid childId, Guid installmentId, CancellationToken ct)
        => Ok(await service.PayChildInstallmentAsync(ParentUserId, childId, installmentId, ct));

    // POST /api/parent/children/{childId}/payments/{paymentId}/confirm
    // Return-path reconcile after Stripe.js resolves (webhook remains authoritative).
    [HttpPost("children/{childId:guid}/payments/{paymentId:guid}/confirm")]
    public async Task<IActionResult> ConfirmPayment(
        Guid childId, Guid paymentId, CancellationToken ct)
    {
        await service.ConfirmChildPaymentAsync(ParentUserId, childId, paymentId, ct);
        return NoContent();
    }
}
