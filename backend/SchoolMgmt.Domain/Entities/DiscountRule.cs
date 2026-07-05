using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Domain.Entities;

public class DiscountRule : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DiscountRuleType RuleType { get; set; }
    public decimal Value { get; set; }
    public Guid? FeeLineItemId { get; set; }
    public FeeLineItem? FeeLineItem { get; set; }
}
