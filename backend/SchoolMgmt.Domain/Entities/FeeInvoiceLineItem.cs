using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class FeeInvoiceLineItem : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeInvoiceId { get; set; }
    public FeeInvoice FeeInvoice { get; set; } = null!;
    public Guid? SourceLineItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public int DisplayOrder { get; set; }
}
