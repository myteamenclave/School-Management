using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class FeeLineItem : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int DisplayOrder { get; set; }
}
