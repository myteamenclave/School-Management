using Microsoft.Extensions.Options;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Payments.Dtos;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.Payments;

// Online fee payment (Stripe test mode). The load-bearing invariant: ReconcilePaymentAsync is the
// ONLY code that marks an installment Paid / a Payment Succeeded, it is idempotent, and it is called
// by both the authoritative webhook and the authenticated return path. See spec 21.
public class PaymentService(
    IFeeInvoiceRepository invoiceRepo,
    IPaymentRepository paymentRepo,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    IDateTimeProvider dateTimeProvider,
    IOptions<StripeOptions> options)
{
    // PHP has 2 minor digits. One home for the conversion — never scatter "* 100".
    private static long ToMinorUnits(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    // AUTHENTICATED path. Re-derives the payable amount server-side (never trusts a client amount),
    // creates a Pending Payment + a Stripe PaymentIntent stamped with tenant/payment/installment ids.
    // expectedStudentId closes the cross-child IDOR: the installment must belong to that student.
    public async Task<InitiatePaymentResult> InitiateInstallmentPaymentAsync(
        Guid installmentId, Guid expectedStudentId, CancellationToken ct = default)
    {
        var installment = await invoiceRepo.GetInstallmentWithInvoiceAsync(installmentId, ct)
            ?? throw new NotFoundException("Installment not found.");

        if (installment.FeeInvoice.StudentId != expectedStudentId)
            throw new NotFoundException("Installment not found."); // not this child's — never leak existence

        if (installment.FeeInvoice.Status != InvoiceStatus.Issued)
            throw new DomainException("Only issued invoices can be paid.");

        var remaining = installment.Amount - (installment.AmountPaid ?? 0m);
        if (remaining <= 0m)
            throw new DomainException("This installment is already paid.");

        var schoolId = tenantProvider.CurrentSchoolId;

        // Payment.Id is assigned at construction (BaseEntity), so we can create the Stripe intent
        // FIRST (metadata references the known id) and persist ONCE with the real intent id — no
        // empty-string window that would collide on the unique StripePaymentIntentId index.
        var payment = new Payment
        {
            FeeInvoiceInstallmentId = installment.Id,
            Amount = remaining,
            Currency = options.Value.Currency,
            Status = PaymentStatus.Pending,
        };

        var intent = await gateway.CreateIntentAsync(
            ToMinorUnits(remaining), payment.Currency,
            new Dictionary<string, string>
            {
                ["schoolId"] = schoolId.ToString(),
                ["paymentId"] = payment.Id.ToString(),
                ["installmentId"] = installment.Id.ToString(),
            }, ct);

        payment.StripePaymentIntentId = intent.PaymentIntentId;
        await paymentRepo.AddAsync(payment, ct);
        await unitOfWork.SaveChangesAsync(ct); // persists Payment + stamps SchoolId (Added path)

        return new InitiatePaymentResult(payment.Id, intent.ClientSecret, options.Value.PublishableKey);
    }

    // AUTHENTICATED return path. Verifies the intent with Stripe (never trusts the client's success
    // claim), then reconciles. expectedSchoolId is the caller's tenant claim; expectedStudentId closes
    // the cross-child IDOR (the payment's installment must belong to that student).
    public async Task ConfirmPaymentAsync(
        Guid paymentId, Guid expectedSchoolId, Guid expectedStudentId, CancellationToken ct = default)
    {
        var payment = await paymentRepo.GetByIdAsync(paymentId, ct)
            ?? throw new NotFoundException("Payment not found.");

        var installment = await invoiceRepo.GetInstallmentWithInvoiceAsync(payment.FeeInvoiceInstallmentId, ct);
        if (installment is null || installment.FeeInvoice.StudentId != expectedStudentId)
            throw new NotFoundException("Payment not found."); // not this child's — never leak existence

        var verified = await gateway.GetIntentAsync(payment.StripePaymentIntentId, ct);
        await ReconcilePaymentAsync(verified, expectedSchoolId, ct);
    }

    // The ONLY writer of Paid/Succeeded. Idempotent: a no-op once the Payment is terminal. Safe under
    // double webhooks, double-clicks, and webhook/return races. Never reads ambient tenant claims —
    // expectedSchoolId is the caller's claim (return path) or the verified metadata (webhook), so it
    // behaves identically in both contexts. The save touches only MODIFIED rows, so AppDbContext never
    // reads CurrentSchoolId — which is what lets the claim-less webhook path save.
    public async Task ReconcilePaymentAsync(
        VerifiedIntent verified, Guid expectedSchoolId, CancellationToken ct = default)
    {
        // Tenant-filter bypass (webhook has no claims); re-scope manually via the assertion below.
        var payment = await paymentRepo.GetByIntentIdIgnoringTenantAsync(verified.PaymentIntentId, ct);
        if (payment is null) return; // unknown intent — nothing to reconcile

        if (payment.SchoolId != expectedSchoolId)
            throw new DomainException("Payment tenant mismatch.");

        if (payment.Status != PaymentStatus.Pending) return; // IDEMPOTENT no-op — already terminal

        if (!verified.Succeeded)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = verified.FailureReason;
            paymentRepo.Update(payment);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var now = dateTimeProvider.UtcNow.UtcDateTime;
        var installment = payment.FeeInvoiceInstallment; // eager-loaded by the ignoring query

        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt = now;

        installment.AmountPaid = payment.Amount; // one installment, full remaining amount
        installment.PaidAt = now;
        installment.Status = InstallmentStatus.Paid;

        paymentRepo.Update(payment);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
