using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class FeeInstallment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public string? DueLabel { get; set; }
    public int DisplayOrder { get; set; }
}
