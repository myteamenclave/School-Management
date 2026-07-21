using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Infrastructure.Tests.FeeInvoices.Fakes;

// Minimal in-memory IFeeInvoiceRepository. Only GetIssuedForStudentAndYearWithDetailsAsync
// carries behaviour; GetStudentFeeOverviewAsync's balance/overdue math is what we isolate.
public class FakeFeeInvoiceRepository : IFeeInvoiceRepository
{
    public List<FeeInvoice> Invoices { get; } = new();

    public void Seed(FeeInvoice invoice) => Invoices.Add(invoice);

    public Task<FeeInvoice?> GetIssuedForStudentAndYearWithDetailsAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        Task.FromResult(Invoices.FirstOrDefault(i =>
            i.StudentId == studentId &&
            i.AcademicYearId == academicYearId &&
            i.Status == InvoiceStatus.Issued));

    public Task<FeeInvoice?> GetActiveForStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<string> GetNextInvoiceCodeAsync(int year, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<FeeInvoice?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<(List<FeeInvoice> Items, int TotalCount)> GetPagedAsync(
        InvoiceStatus? status, Guid? gradeId, Guid? academicYearId,
        Guid? studentId, int page, int pageSize, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<FeeInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task AddAsync(FeeInvoice entity, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Update(FeeInvoice entity) => throw new NotSupportedException();

    public void Remove(FeeInvoice entity) => throw new NotSupportedException();
}
