using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Infrastructure.Persistence;

namespace SchoolMgmt.IntegrationTests.TestSupport;

// Adds a probe entity to AppDbContext's model so the real reflection-based
// tenant query filter can be exercised against a real Postgres instance
// without a real business entity existing yet.
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
