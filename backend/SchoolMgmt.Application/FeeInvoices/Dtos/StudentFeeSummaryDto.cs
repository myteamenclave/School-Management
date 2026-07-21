namespace SchoolMgmt.Application.FeeInvoices.Dtos;

// Server-computed balance rollup for one student+year. All money is decimal.
// When there is no Issued invoice: HasInvoice=false, all amounts 0, all nullable fields null,
// OverdueInstallmentIds empty. Overdue is derived at read time (not persisted) and surfaced as
// a set of installment ids so the UI never recomputes the money rule (spec 20).
public record StudentFeeSummaryDto(
    bool HasInvoice,
    decimal TotalBilled,        // invoice.TotalAmount (sum of installment amounts)
    decimal TotalPaid,          // Σ (installment.AmountPaid ?? 0) — always 0 until pay-online
    decimal Outstanding,        // TotalBilled - TotalPaid
    DateOnly? NextDueDate,      // earliest UNPAID installment's DueDate (nulls-last); null if none
    decimal? NextDueAmount,     // that installment's remaining amount; null if none
    decimal OverdueAmount,      // Σ remaining amount of installments past due and not fully paid
    int OverdueCount,           // count of those installments
    List<Guid> OverdueInstallmentIds);  // ids the UI highlights as past-due (one overdue definition)
