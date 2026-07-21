namespace SchoolMgmt.Application.FeeInvoices.Dtos;

// The parent fee payload: the summary hero + the Issued invoice (null when none issued).
// Invoice reuses the existing FeeInvoiceDto (line items + installments) unchanged (spec 20).
public record StudentFeeOverviewDto(
    StudentFeeSummaryDto Summary,
    FeeInvoiceDto? Invoice);
