namespace SchoolMgmt.Application.Payments;

// A newly created payment intent — the client secret drives Stripe Elements on the frontend.
public record CreatedIntent(string PaymentIntentId, string ClientSecret);

// The verified outcome of an intent, from either a retrieve (return path) or a webhook event.
// Metadata carries schoolId/paymentId/installmentId stamped at creation.
public record VerifiedIntent(
    string PaymentIntentId,
    bool Succeeded,
    string? FailureReason,
    IReadOnlyDictionary<string, string> Metadata);

// Thin seam over the payment provider (Stripe). Keeps Stripe.net out of the Application layer
// and makes PaymentService unit-testable with a fake. NOT a multi-provider abstraction.
public interface IPaymentGateway
{
    // amountMinorUnits = pesos * 100 (integer). metadata carries schoolId/paymentId/installmentId.
    Task<CreatedIntent> CreateIntentAsync(
        long amountMinorUnits, string currency, IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    // Retrieve + report status. Used by the return path so a client "it succeeded" is never trusted.
    Task<VerifiedIntent> GetIntentAsync(string paymentIntentId, CancellationToken ct = default);

    // Verify the Stripe-Signature header against the raw body and parse the event. Returns the
    // intent for payment_intent.succeeded / .payment_failed, else null (event we don't handle).
    // Throws when the signature is invalid.
    VerifiedIntent? ParseWebhookEvent(string rawJson, string stripeSignatureHeader);
}
