using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMgmt.Application.Payments;

namespace SchoolMgmt.WebApi.Controllers;

// Stripe → us, server-to-server. Carries NO cookie/JWT, so it is [AllowAnonymous] and authenticated
// by the Stripe signature (HMAC over the raw body) instead — see spec 21 / docs/ideas/18. The tenant
// is resolved from the verified intent metadata, not from claims. This is the ONLY anonymous endpoint.
[ApiController]
[Route("api/webhooks/stripe")]
[AllowAnonymous]
public class StripeWebhookController(IPaymentGateway gateway, PaymentService payments) : ControllerBase
{
    // POST /api/webhooks/stripe
    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        // Raw body is required for signature verification — do NOT model-bind (that consumes the stream).
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        VerifiedIntent? verified;
        try
        {
            verified = gateway.ParseWebhookEvent(rawJson, signature);
        }
        catch
        {
            return BadRequest(); // bad/forged signature — no state touched
        }

        if (verified is null)
            return Ok(); // event we don't handle — 200 so Stripe stops retrying

        if (!verified.Metadata.TryGetValue("schoolId", out var schoolIdRaw)
            || !Guid.TryParse(schoolIdRaw, out var schoolId))
            return Ok(); // intent not created by us / missing metadata — acknowledge, do nothing

        await payments.ReconcilePaymentAsync(verified, schoolId, ct);
        return Ok(); // 200 tells Stripe the event was delivered
    }
}
