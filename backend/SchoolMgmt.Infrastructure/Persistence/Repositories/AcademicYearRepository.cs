using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Repositories;

internal sealed class AcademicYearRepository : Repository<AcademicYear>, IAcademicYearRepository
{
    private readonly AppDbContext _context;

    public AcademicYearRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }

    public Task<List<AcademicYear>> GetAllWithSemestersAsync(CancellationToken ct = default) =>
        DbSet.Include(y => y.Semesters).OrderByDescending(y => y.StartDate).ToListAsync(ct);

    public Task<AcademicYear?> GetWithSemestersAsync(Guid id, CancellationToken ct = default) =>
        DbSet.Include(y => y.Semesters).FirstOrDefaultAsync(y => y.Id == id, ct);

    public Task<AcademicYear?> GetCurrentAsync(CancellationToken ct = default) =>
        DbSet.Include(y => y.Semesters).FirstOrDefaultAsync(y => y.IsCurrent, ct);

    public Task<Semester?> GetCurrentSemesterAsync(CancellationToken ct = default) =>
        _context.Set<Semester>().FirstOrDefaultAsync(s => s.IsCurrent, ct);

    public Task<Semester?> GetSemesterByIdAsync(Guid semesterId, CancellationToken ct = default) =>
        _context.Set<Semester>().FirstOrDefaultAsync(s => s.Id == semesterId, ct);

    public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) =>
        DbSet.AnyAsync(y => y.Name == name, ct);
}
