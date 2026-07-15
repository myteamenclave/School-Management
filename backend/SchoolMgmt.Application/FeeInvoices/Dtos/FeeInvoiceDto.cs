namespace SchoolMgmt.Application.FeeInvoices.Dtos;

public record FeeInvoiceDto(
    Guid Id, string InvoiceCode,
    Guid StudentId, string StudentName, string StudentCode,
    Guid AcademicYearId, string AcademicYearName,
    Guid FeeTemplateId, string TemplateName,
    decimal TotalAmount, string Status,
    DateTime? IssuedAt, DateTime? CancelledAt,
    DateTime CreatedAt, DateTime? UpdatedAt,
    List<FeeInvoiceLineItemDto> LineItems,
    List<FeeInvoiceInstallmentDto> Installments);

public record FeeInvoiceLineItemDto(
    Guid Id, string Name,
    decimal OriginalAmount, decimal DiscountAmount, decimal FinalAmount,
    int DisplayOrder);

public record FeeInvoiceInstallmentDto(
    Guid Id, string Name, decimal Percentage,
    DateOnly? DueDate, decimal Amount, string Status, int DisplayOrder);
