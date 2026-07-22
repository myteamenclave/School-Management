using Microsoft.Extensions.Options;
using SchoolMgmt.Application.Payments;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;
using SchoolMgmt.Infrastructure.Tests.Auth.Fakes;
using SchoolMgmt.Infrastructure.Tests.Fakes;
using SchoolMgmt.Infrastructure.Tests.FeeInvoices.Fakes;
using SchoolMgmt.Infrastructure.Tests.Payments.Fakes;

namespace SchoolMgmt.Infrastructure.Tests.Payments;

// PaymentService owns the load-bearing invariants (spec 21): ReconcilePaymentAsync is the ONLY
// writer of Paid/Succeeded and is idempotent; the amount is re-derived server-side; the tenant is
// asserted against the verified metadata. These isolate that logic with fakes.
public class PaymentServiceTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static (PaymentService svc, FakePaymentRepository payments, FakeFeeInvoiceRepository invoices, FakePaymentGateway gateway)
        Build()
    {
        var payments = new FakePaymentRepository();
        var invoices = new FakeFeeInvoiceRepository();
        var gateway = new FakePaymentGateway();
        var svc = new PaymentService(
            invoices, payments, gateway,
            new FakeUnitOfWork(),
            new FakeTenantProvider(SchoolId),
            new FakeDateTimeProvider(Now),
            Options.Create(new StripeOptions { Currency = "PHP", PublishableKey = "pk_test_x" }));
        return (svc, payments, invoices, gateway);
    }

    private static Payment PendingPayment(FeeInvoiceInstallment installment, decimal amount, string intentId) =>
        new()
        {
            SchoolId = SchoolId,
            FeeInvoiceInstallmentId = installment.Id,
            FeeInvoiceInstallment = installment,
            Amount = amount,
            Currency = "PHP",
            Status = PaymentStatus.Pending,
            StripePaymentIntentId = intentId,
        };

    private static FeeInvoiceInstallment Installment(decimal amount, decimal? paid = null) => new()
    {
        Id = Guid.NewGuid(),
        FeeInvoiceId = InvoiceId,
        Name = "1st",
        Amount = amount,
        AmountPaid = paid,
        Status = InstallmentStatus.Pending,
    };

    private static FeeInvoiceInstallment IssuedInstallment(decimal amount, decimal? paid = null)
    {
        var inst = Installment(amount, paid);
        inst.FeeInvoice = new FeeInvoice
        {
            Id = InvoiceId,
            StudentId = StudentId,
            Status = InvoiceStatus.Issued,
        };
        return inst;
    }

    private static VerifiedIntent Intent(string id, bool ok, string? reason = null) =>
        new(id, ok, reason, new Dictionary<string, string>());

    // ─── Reconcile ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Reconcile_Succeeded_MarksInstallmentPaid()
    {
        var (svc, payments, _, _) = Build();
        var inst = Installment(600m);
        var payment = PendingPayment(inst, 600m, "pi_1");
        payments.Seed(payment);

        await svc.ReconcilePaymentAsync(Intent("pi_1", ok: true), SchoolId);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(Now.UtcDateTime, payment.PaidAt);
        Assert.Equal(InstallmentStatus.Paid, inst.Status);
        Assert.Equal(600m, inst.AmountPaid);
        Assert.Equal(Now.UtcDateTime, inst.PaidAt);
    }

    [Fact]
    public async Task Reconcile_SameEventTwice_IsIdempotent()
    {
        var (svc, payments, _, _) = Build();
        var inst = Installment(600m);
        var payment = PendingPayment(inst, 600m, "pi_1");
        payments.Seed(payment);

        await svc.ReconcilePaymentAsync(Intent("pi_1", ok: true), SchoolId);
        // Second delivery of the same event — must be a no-op (still Succeeded, still 600 paid once).
        await svc.ReconcilePaymentAsync(Intent("pi_1", ok: true), SchoolId);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(600m, inst.AmountPaid);
        Assert.Single(payments.Payments);
    }

    [Fact]
    public async Task Reconcile_Failed_MarksPaymentFailed_InstallmentUntouched()
    {
        var (svc, payments, _, _) = Build();
        var inst = Installment(600m);
        var payment = PendingPayment(inst, 600m, "pi_1");
        payments.Seed(payment);

        await svc.ReconcilePaymentAsync(Intent("pi_1", ok: false, reason: "card_declined"), SchoolId);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("card_declined", payment.FailureReason);
        Assert.Equal(InstallmentStatus.Pending, inst.Status);
        Assert.Null(inst.AmountPaid);
    }

    [Fact]
    public async Task Reconcile_UnknownIntent_IsNoOp()
    {
        var (svc, _, _, _) = Build();
        // No seeded payment — must not throw.
        await svc.ReconcilePaymentAsync(Intent("pi_missing", ok: true), SchoolId);
    }

    [Fact]
    public async Task Reconcile_SchoolIdMismatch_Throws_NothingWritten()
    {
        var (svc, payments, _, _) = Build();
        var inst = Installment(600m);
        var payment = PendingPayment(inst, 600m, "pi_1");
        payments.Seed(payment);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.ReconcilePaymentAsync(Intent("pi_1", ok: true), Guid.NewGuid()));

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(InstallmentStatus.Pending, inst.Status);
    }

    // ─── Initiate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Initiate_IssuedUnpaid_CreatesIntentWithReDerivedAmountAndMetadata()
    {
        var (svc, payments, invoices, gateway) = Build();
        var inst = IssuedInstallment(1234.56m);
        invoices.SeedInstallment(inst);

        var result = await svc.InitiateInstallmentPaymentAsync(inst.Id, StudentId);

        Assert.Equal(123456, gateway.LastAmountMinorUnits); // pesos * 100, server-derived
        Assert.Equal(SchoolId.ToString(), gateway.LastMetadata!["schoolId"]);
        Assert.Equal(inst.Id.ToString(), gateway.LastMetadata!["installmentId"]);
        Assert.Equal("pk_test_x", result.PublishableKey);
        var payment = Assert.Single(payments.Payments);
        Assert.Equal(gateway.NextIntentId, payment.StripePaymentIntentId);
        Assert.Equal(1234.56m, payment.Amount);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task Initiate_AlreadyPaidInstallment_Throws()
    {
        var (svc, _, invoices, _) = Build();
        var inst = IssuedInstallment(600m, paid: 600m);
        invoices.SeedInstallment(inst);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.InitiateInstallmentPaymentAsync(inst.Id, StudentId));
    }

    [Fact]
    public async Task Initiate_NonIssuedInvoice_Throws()
    {
        var (svc, _, invoices, _) = Build();
        var inst = IssuedInstallment(600m);
        inst.FeeInvoice.Status = InvoiceStatus.Draft;
        invoices.SeedInstallment(inst);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.InitiateInstallmentPaymentAsync(inst.Id, StudentId));
    }

    [Fact]
    public async Task Initiate_InstallmentNotThisChild_Throws404()
    {
        var (svc, _, invoices, _) = Build();
        var inst = IssuedInstallment(600m); // belongs to StudentId
        invoices.SeedInstallment(inst);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.InitiateInstallmentPaymentAsync(inst.Id, Guid.NewGuid())); // different child
    }
}
