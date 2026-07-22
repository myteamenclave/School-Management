using System.Text.Json;
using SchoolMgmt.Application.Payments;

namespace SchoolMgmt.IntegrationTests.Fakes;

// Replaces StripePaymentGateway in the integration host so tests never hit real Stripe.
// Registered as a singleton, so metadata captured at CreateIntent survives to GetIntent/webhook
// within one test. The webhook body is a tiny test contract: { "paymentIntentId", "succeeded" };
// a "valid" Stripe-Signature header is accepted, anything else throws (proves the 400 path).
public class FakeStripePaymentGateway : IPaymentGateway
{
    public const string ValidSignature = "valid";

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _metadataByIntent = new();
    private int _counter;

    // Controls what GetIntentAsync (return path) reports.
    public bool ReturnPathSucceeds { get; set; } = true;

    public string LastCreatedIntentId { get; private set; } = string.Empty;
    public long LastAmountMinorUnits { get; private set; }

    public Task<CreatedIntent> CreateIntentAsync(
        long amountMinorUnits, string currency, IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var id = $"pi_test_{++_counter}_{Guid.NewGuid():N}";
        _metadataByIntent[id] = metadata;
        LastCreatedIntentId = id;
        LastAmountMinorUnits = amountMinorUnits;
        return Task.FromResult(new CreatedIntent(id, id + "_secret"));
    }

    public Task<VerifiedIntent> GetIntentAsync(string paymentIntentId, CancellationToken ct = default)
    {
        var metadata = _metadataByIntent.GetValueOrDefault(paymentIntentId)
            ?? new Dictionary<string, string>();
        return Task.FromResult(new VerifiedIntent(
            paymentIntentId, ReturnPathSucceeds, ReturnPathSucceeds ? null : "card_declined", metadata));
    }

    public VerifiedIntent? ParseWebhookEvent(string rawJson, string stripeSignatureHeader)
    {
        if (stripeSignatureHeader != ValidSignature)
            throw new InvalidOperationException("Invalid signature.");

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        var intentId = root.GetProperty("paymentIntentId").GetString()!;
        var succeeded = root.GetProperty("succeeded").GetBoolean();

        var metadata = _metadataByIntent.GetValueOrDefault(intentId)
            ?? new Dictionary<string, string>();
        return new VerifiedIntent(intentId, succeeded, succeeded ? null : "card_declined", metadata);
    }
}
