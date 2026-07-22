using SchoolMgmt.Application.Payments;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Infrastructure.Tests.Payments.Fakes;

// In-memory IPaymentRepository. The "ignoring tenant" lookup returns the same rows as the scoped
// one here — the bypass semantics are exercised in integration tests against real Postgres.
public class FakePaymentRepository : IPaymentRepository
{
    public List<Payment> Payments { get; } = new();

    public void Seed(Payment payment) => Payments.Add(payment);

    public Task<Payment?> GetByIntentIdAsync(string paymentIntentId, CancellationToken ct = default) =>
        Task.FromResult(Payments.FirstOrDefault(p => p.StripePaymentIntentId == paymentIntentId));

    public Task<Payment?> GetByIntentIdIgnoringTenantAsync(string paymentIntentId, CancellationToken ct = default) =>
        Task.FromResult(Payments.FirstOrDefault(p => p.StripePaymentIntentId == paymentIntentId));

    public Task<bool> AnySucceededForInvoiceAsync(Guid feeInvoiceId, CancellationToken ct = default) =>
        Task.FromResult(Payments.Any(p =>
            p.Status == PaymentStatus.Succeeded &&
            p.FeeInvoiceInstallment.FeeInvoiceId == feeInvoiceId));

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Payments.FirstOrDefault(p => p.Id == id));

    public Task AddAsync(Payment entity, CancellationToken cancellationToken = default)
    {
        Payments.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Payment entity) { /* tracked in-memory; no-op */ }

    public void Remove(Payment entity) => Payments.Remove(entity);
}
