using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class GradeRepository : Repository<Grade>, IGradeRepository
{
    private readonly AppDbContext _context;

    public GradeRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }
    public Task<List<Grade>> GetAllWithSectionsAsync(CancellationToken ct = default) =>
        DbSet.Include(g => g.Sections).OrderBy(g => g.DisplayOrder).ToListAsync(ct);

    public Task<Grade?> GetWithSectionsAsync(Guid id, CancellationToken ct = default) =>
        DbSet.Include(g => g.Sections).FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<bool> GradeNameExistsAsync(string name, CancellationToken ct = default) =>
        DbSet.AnyAsync(g => g.Name == name, ct);

    public Task<Section?> GetSectionAsync(Guid gradeId, Guid sectionId, CancellationToken ct = default) =>
        _context.Set<Section>().FirstOrDefaultAsync(s => s.GradeId == gradeId && s.Id == sectionId, ct);

    public Task<bool> SectionNameExistsInGradeAsync(Guid gradeId, string name, CancellationToken ct = default) =>
        _context.Set<Section>().AnyAsync(s => s.GradeId == gradeId && s.Name == name, ct);

    public Task AddSectionAsync(Section section, CancellationToken ct = default)
    {
        _context.Set<Section>().Add(section);
        return Task.CompletedTask;
    }

    public void RemoveSection(Section section) => _context.Set<Section>().Remove(section);
}
