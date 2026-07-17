using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

// Admin-editable letter band (idea doc 12). A score maps to the band whose
// [MinScore, MaxScore] range contains it. One flat set of rows per school.
public class GradeScaleBand : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Letter { get; set; } = string.Empty;   // "A", "B+", ...
    public decimal MinScore { get; set; }                 // inclusive
    public decimal MaxScore { get; set; }                 // inclusive
}
