using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Payments;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository(AppDbContext context)
    : Repository<Payment>(context), IPaymentRepository
{
    public Task<Payment?> GetByIntentIdAsync(string paymentIntentId, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, ct);

    // TENANT-FILTER BYPASS — webhook/reconcile only (no JWT ⇒ no claims ⇒ the global filter would
    // throw). The intent id is globally unique; the caller (PaymentService.ReconcilePaymentAsync)
    // asserts the returned Payment.SchoolId matches the verified intent metadata. Eager-loads the
    // installment so reconcile can mutate it without a second, claim-dependent query.
    public Task<Payment?> GetByIntentIdIgnoringTenantAsync(
        string paymentIntentId, CancellationToken ct = default) =>
        DbSet
            .IgnoreQueryFilters()
            .Include(p => p.FeeInvoiceInstallment)
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, ct);

    // Tenant-scoped — Cancel guard. Any Succeeded payment on any installment of this invoice?
    public Task<bool> AnySucceededForInvoiceAsync(Guid feeInvoiceId, CancellationToken ct = default) =>
        DbSet.AnyAsync(p =>
            p.Status == PaymentStatus.Succeeded &&
            p.FeeInvoiceInstallment.FeeInvoiceId == feeInvoiceId, ct);
}
