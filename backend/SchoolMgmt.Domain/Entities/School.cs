using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class School : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
