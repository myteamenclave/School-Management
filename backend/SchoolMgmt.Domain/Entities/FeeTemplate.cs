using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class FeeTemplate : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public Guid GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsFrozen { get; set; } = false;

    private readonly List<FeeLineItem> _lineItems = [];
    public IReadOnlyList<FeeLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<FeeInstallment> _installments = [];
    public IReadOnlyList<FeeInstallment> Installments => _installments.AsReadOnly();

    private readonly List<DiscountRule> _discountRules = [];
    public IReadOnlyList<DiscountRule> DiscountRules => _discountRules.AsReadOnly();
}
