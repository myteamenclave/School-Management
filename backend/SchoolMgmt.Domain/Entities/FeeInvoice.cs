using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Domain.Entities;

public class FeeInvoice : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime? IssuedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    private readonly List<FeeInvoiceLineItem> _lineItems = [];
    public IReadOnlyList<FeeInvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<FeeInvoiceInstallment> _installments = [];
    public IReadOnlyList<FeeInvoiceInstallment> Installments => _installments.AsReadOnly();
}
