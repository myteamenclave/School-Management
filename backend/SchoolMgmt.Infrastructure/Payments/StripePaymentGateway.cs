using Microsoft.Extensions.Options;
using SchoolMgmt.Application.Payments;
using Stripe;

namespace SchoolMgmt.Infrastructure.Payments;

// Concrete Stripe implementation of the IPaymentGateway seam. The only place Stripe.net is referenced.
// Constructs a per-instance StripeClient from IOptions<StripeOptions> (no global StripeConfiguration).
internal sealed class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    private readonly StripeOptions _options = options.Value;
    private PaymentIntentService? _intentsCache;

    // Built lazily on first real use — NOT in the constructor — so DI resolution never fails when
    // the secret key is unset (e.g. tests / non-payment requests that transitively resolve this).
    // A StripeClient with an empty key throws; an actual payment attempt should fail loudly, but
    // merely wiring up the object graph must not.
    private PaymentIntentService Intents =>
        _intentsCache ??= new PaymentIntentService(new StripeClient(_options.SecretKey));

    public async Task<CreatedIntent> CreateIntentAsync(
        long amountMinorUnits, string currency, IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var intent = await Intents.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amountMinorUnits,
            Currency = currency.ToLowerInvariant(),
            Metadata = new Dictionary<string, string>(metadata),
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
        }, cancellationToken: ct);

        return new CreatedIntent(intent.Id, intent.ClientSecret);
    }

    public async Task<VerifiedIntent> GetIntentAsync(string paymentIntentId, CancellationToken ct = default)
    {
        var intent = await Intents.GetAsync(paymentIntentId, cancellationToken: ct);
        return ToVerified(intent);
    }

    public VerifiedIntent? ParseWebhookEvent(string rawJson, string stripeSignatureHeader)
    {
        // Throws StripeException on a bad/forged signature — the controller maps that to 400.
        var stripeEvent = EventUtility.ConstructEvent(
            rawJson, stripeSignatureHeader, _options.WebhookSigningSecret);

        if (stripeEvent.Type is not (EventTypes.PaymentIntentSucceeded or EventTypes.PaymentIntentPaymentFailed))
            return null; // event we don't handle

        if (stripeEvent.Data.Object is not PaymentIntent intent)
            return null;

        return ToVerified(intent);
    }

    private static VerifiedIntent ToVerified(PaymentIntent intent) => new(
        intent.Id,
        intent.Status == "succeeded",
        intent.LastPaymentError?.Message,
        intent.Metadata ?? new Dictionary<string, string>());
}
