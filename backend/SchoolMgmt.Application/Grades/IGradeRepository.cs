using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Grades;

public interface IGradeRepository : IRepository<Grade>
{
    Task<List<Grade>> GetAllWithSectionsAsync(CancellationToken ct = default);
    Task<Grade?> GetWithSectionsAsync(Guid id, CancellationToken ct = default);
    Task<bool> GradeNameExistsAsync(string name, CancellationToken ct = default);
    Task<Section?> GetSectionAsync(Guid gradeId, Guid sectionId, CancellationToken ct = default);
    Task<bool> SectionNameExistsInGradeAsync(Guid gradeId, string name, CancellationToken ct = default);
    Task AddSectionAsync(Section section, CancellationToken ct = default);
    void RemoveSection(Section section);
}
