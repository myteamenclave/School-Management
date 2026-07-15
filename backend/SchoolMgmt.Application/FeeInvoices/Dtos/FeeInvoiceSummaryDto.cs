namespace SchoolMgmt.Application.FeeInvoices.Dtos;

public record FeeInvoiceSummaryDto(
    Guid Id, string InvoiceCode,
    Guid StudentId, string StudentName, string StudentCode,
    Guid AcademicYearId, string AcademicYearName,
    Guid FeeTemplateId, string TemplateName,
    decimal TotalAmount, string Status,
    DateTime? IssuedAt, DateTime CreatedAt);
