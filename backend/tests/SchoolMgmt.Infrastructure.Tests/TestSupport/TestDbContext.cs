using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Infrastructure.Persistence;

namespace SchoolMgmt.Infrastructure.Tests.TestSupport;

// Adds a probe entity to AppDbContext's model so its real OnModelCreating
// (incl. the reflection-based tenant query filter) and SaveChangesAsync
// override can be exercised without a real business entity existing yet.
public class TestDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantProvider tenantProvider,
    IDateTimeProvider dateTimeProvider)
    : AppDbContext(options, tenantProvider, dateTimeProvider)
{
    public DbSet<ProbeEntity> Probes => Set<ProbeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProbeEntity>();
        base.OnModelCreating(modelBuilder);
    }
}
