using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Infrastructure.Tests.Fakes;
using SchoolMgmt.Infrastructure.Tests.TestSupport;

namespace SchoolMgmt.Infrastructure.Tests.Persistence;

public class AppDbContextSaveChangesTests
{
    private static TestDbContext CreateContext(Guid tenantId, DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<global::SchoolMgmt.Infrastructure.Persistence.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options, new FakeTenantProvider(tenantId), new FakeDateTimeProvider(now));
    }

    [Fact]
    public async Task AddingEntity_StampsCreatedAt()
    {
        var now = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId, now);

        var probe = new ProbeEntity { Name = "Probe 1" };
        context.Probes.Add(probe);
        await context.SaveChangesAsync();

        Assert.Equal(now, probe.CreatedAt);
        Assert.Null(probe.UpdatedAt);
    }

    [Fact]
    public async Task AddingTenantScopedEntity_WithoutExplicitSchoolId_StampsCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId, DateTimeOffset.UtcNow);

        var probe = new ProbeEntity { Name = "Probe 1" };
        context.Probes.Add(probe);
        await context.SaveChangesAsync();

        Assert.Equal(tenantId, probe.SchoolId);
    }

    [Fact]
    public async Task AddingTenantScopedEntity_WithExplicitSchoolId_DoesNotOverwriteIt()
    {
        var tenantId = Guid.NewGuid();
        var explicitSchoolId = Guid.NewGuid();
        await using var context = CreateContext(tenantId, DateTimeOffset.UtcNow);

        var probe = new ProbeEntity { Name = "Probe 1", SchoolId = explicitSchoolId };
        context.Probes.Add(probe);
        await context.SaveChangesAsync();

        Assert.Equal(explicitSchoolId, probe.SchoolId);
        Assert.NotEqual(tenantId, probe.SchoolId);
    }

    [Fact]
    public async Task UpdatingEntity_StampsUpdatedAt_AndLeavesCreatedAtUnchanged()
    {
        var createdAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var updatedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var tenantId = Guid.NewGuid();
        var dateTimeProvider = new FakeDateTimeProvider(createdAt);

        var options = new DbContextOptionsBuilder<global::SchoolMgmt.Infrastructure.Persistence.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var probeId = Guid.NewGuid();
        await using (var context = new TestDbContext(options, new FakeTenantProvider(tenantId), dateTimeProvider))
        {
            context.Probes.Add(new ProbeEntity { Id = probeId, Name = "Original" });
            await context.SaveChangesAsync();
        }

        dateTimeProvider.UtcNow = updatedAt;
        await using (var context = new TestDbContext(options, new FakeTenantProvider(tenantId), dateTimeProvider))
        {
            var probe = await context.Probes.SingleAsync(p => p.Id == probeId);
            probe.Name = "Renamed";
            await context.SaveChangesAsync();

            Assert.Equal(createdAt, probe.CreatedAt);
            Assert.Equal(updatedAt, probe.UpdatedAt);
        }
    }
}
