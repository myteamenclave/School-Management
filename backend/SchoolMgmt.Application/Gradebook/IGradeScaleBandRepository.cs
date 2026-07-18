using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Gradebook;

public interface IGradeScaleBandRepository : IRepository<GradeScaleBand>
{
    // All bands ordered MinScore descending (highest band first).
    Task<List<GradeScaleBand>> GetAllOrderedAsync(CancellationToken ct = default);
}
