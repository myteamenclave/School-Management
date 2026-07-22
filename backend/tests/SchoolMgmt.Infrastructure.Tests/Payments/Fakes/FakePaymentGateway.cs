using SchoolMgmt.Application.Payments;

namespace SchoolMgmt.Infrastructure.Tests.Payments.Fakes;

// Configurable in-memory IPaymentGateway for PaymentService unit tests — no real Stripe calls.
public class FakePaymentGateway : IPaymentGateway
{
    public long LastAmountMinorUnits { get; private set; }
    public IReadOnlyDictionary<string, string>? LastMetadata { get; private set; }
    public string NextIntentId { get; set; } = "pi_test_123";

    // Controls what GetIntentAsync (return path) reports.
    public bool IntentSucceeded { get; set; } = true;
    public string? IntentFailureReason { get; set; }

    public Task<CreatedIntent> CreateIntentAsync(
        long amountMinorUnits, string currency, IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        LastAmountMinorUnits = amountMinorUnits;
        LastMetadata = metadata;
        return Task.FromResult(new CreatedIntent(NextIntentId, NextIntentId + "_secret"));
    }

    public Task<VerifiedIntent> GetIntentAsync(string paymentIntentId, CancellationToken ct = default) =>
        Task.FromResult(new VerifiedIntent(
            paymentIntentId, IntentSucceeded, IntentFailureReason,
            LastMetadata ?? new Dictionary<string, string>()));

    public VerifiedIntent? ParseWebhookEvent(string rawJson, string stripeSignatureHeader) =>
        throw new NotSupportedException("Webhook parsing is exercised in integration tests.");
}
