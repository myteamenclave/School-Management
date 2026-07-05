using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.FeeTemplates;

public interface IFeeTemplateRepository : IRepository<FeeTemplate>
{
    Task<(List<FeeTemplate> Items, int TotalCount)> GetPagedAsync(
        Guid? academicYearId, Guid? gradeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);

    Task<FeeTemplate?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default);
}
