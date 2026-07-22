using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Payments;

public interface IPaymentRepository : IRepository<Payment>
{
    // Tenant-scoped — used by the authenticated initiate/confirm paths.
    Task<Payment?> GetByIntentIdAsync(string paymentIntentId, CancellationToken ct = default);

    // TENANT-FILTER BYPASS — webhook/reconcile ONLY. The webhook has no JWT, so the global query
    // filter (which reads ITenantProvider.CurrentSchoolId) would throw. This looks the Payment up
    // by its globally-unique Stripe intent id with the filter disabled; the caller MUST then assert
    // the returned Payment.SchoolId equals the SchoolId in the verified intent metadata. Eager-loads
    // the installment (+ its invoice) so reconcile can mutate without a claim-dependent follow-up query.
    Task<Payment?> GetByIntentIdIgnoringTenantAsync(string paymentIntentId, CancellationToken ct = default);

    // Tenant-scoped — used by the Cancel guard: is there a Succeeded payment on any installment of this invoice?
    Task<bool> AnySucceededForInvoiceAsync(Guid feeInvoiceId, CancellationToken ct = default);
}
