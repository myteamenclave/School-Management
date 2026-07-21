using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.FeeInvoices;

public interface IFeeInvoiceRepository : IRepository<FeeInvoice>
{
    Task<string> GetNextInvoiceCodeAsync(int year, CancellationToken ct = default);

    Task<FeeInvoice?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<FeeInvoice?> GetActiveForStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    // The single Issued invoice for a student+year, with full details (line items + installments).
    // At most one row exists (one active invoice per student per year) — parent portal read path.
    Task<FeeInvoice?> GetIssuedForStudentAndYearWithDetailsAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<(List<FeeInvoice> Items, int TotalCount)> GetPagedAsync(
        InvoiceStatus? status, Guid? gradeId, Guid? academicYearId,
        Guid? studentId, int page, int pageSize, CancellationToken ct = default);
}
