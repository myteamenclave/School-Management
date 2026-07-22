namespace SchoolMgmt.Application.Payments;

// Bound from the "Stripe" config section. Secret/webhook values MUST come from
// user-secrets / environment variables (Stripe__SecretKey, Stripe__WebhookSigningSecret),
// never committed to appsettings.json. PublishableKey is public and served to the client.
public class StripeOptions
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSigningSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "PHP";
}
