using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Domain.Entities;

public class FeeInvoiceInstallment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeInvoiceId { get; set; }
    public FeeInvoice FeeInvoice { get; set; } = null!;
    public Guid? SourceInstallmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Amount { get; set; }
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;
    public decimal? AmountPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public int DisplayOrder { get; set; }
}
