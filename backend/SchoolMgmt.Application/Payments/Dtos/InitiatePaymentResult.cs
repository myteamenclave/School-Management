namespace SchoolMgmt.Application.Payments.Dtos;

// Returned to the parent when a payment is initiated. ClientSecret + PublishableKey drive
// the Stripe Elements card form inline on the frontend (no redirect). PaymentId is echoed back
// on the confirm call so the return path can reconcile the exact payment.
public record InitiatePaymentResult(Guid PaymentId, string ClientSecret, string PublishableKey);
