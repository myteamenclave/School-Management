using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Domain.Entities;

// One row per Stripe PaymentIntent — the collections audit trail for online fee payments.
// StripePaymentIntentId is the idempotency key: reconciliation is keyed on it and it is unique.
// Failed rows are kept (audit), never pruned.
public class Payment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeInvoiceInstallmentId { get; set; }
    public FeeInvoiceInstallment FeeInvoiceInstallment { get; set; } = null!;
    public decimal Amount { get; set; }                 // remaining amount snapshot at initiation
    public string Currency { get; set; } = "PHP";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime? PaidAt { get; set; }
}
