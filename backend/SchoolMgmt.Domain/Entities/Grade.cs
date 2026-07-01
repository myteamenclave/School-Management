using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class Grade : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    private readonly List<Section> _sections = [];
    public IReadOnlyList<Section> Sections => _sections.AsReadOnly();

    public void EnsureNoSections()
    {
        if (_sections.Count > 0)
            throw new DomainException("Cannot delete a grade that still has sections. Delete all sections first.");
    }
}
