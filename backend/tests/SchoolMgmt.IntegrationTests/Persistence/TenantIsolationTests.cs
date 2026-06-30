using Microsoft.EntityFrameworkCore;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Persistence;

[Collection(IntegrationTestCollection.Name)]
public class TenantIsolationTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task Query_OnlyReturnsRows_ForTheCurrentTenant()
    {
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        var connectionString = await fixture.CreateProbeDatabaseAsync();

        await using (var contextA = PostgresContainerFixture.CreateProbeDbContext(connectionString, schoolA))
        {
            contextA.Probes.Add(new ProbeEntity { Name = "School A - Student 1" });
            contextA.Probes.Add(new ProbeEntity { Name = "School A - Student 2" });
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = PostgresContainerFixture.CreateProbeDbContext(connectionString, schoolB))
        {
            contextB.Probes.Add(new ProbeEntity { Name = "School B - Student 1" });
            await contextB.SaveChangesAsync();
        }

        await using var queryAsSchoolA = PostgresContainerFixture.CreateProbeDbContext(connectionString, schoolA);
        var visibleToA = await queryAsSchoolA.Probes.ToListAsync();

        Assert.Equal(2, visibleToA.Count);
        Assert.All(visibleToA, p => Assert.Equal(schoolA, p.SchoolId));

        await using var queryAsSchoolB = PostgresContainerFixture.CreateProbeDbContext(connectionString, schoolB);
        var visibleToB = await queryAsSchoolB.Probes.ToListAsync();

        Assert.Single(visibleToB);
        Assert.Equal(schoolB, visibleToB[0].SchoolId);
    }

    [Fact]
    public async Task IgnoreQueryFilters_BypassesTenantScoping_ForSetupOrAdminUseOnly()
    {
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        var connectionString = await fixture.CreateProbeDatabaseAsync();

        await using (var contextA = PostgresContainerFixture.CreateProbeDbContext(connectionString, schoolA))
        {
            contextA.Probes.Add(new ProbeEntity { Name = "School A - Student 1" });
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = PostgresContainerFixture.CreateProbeDbContext(connectionString, schoolB))
        {
            contextB.Probes.Add(new ProbeEntity { Name = "School B - Student 1" });
            await contextB.SaveChangesAsync();
        }

        await using var context = PostgresContainerFixture.CreateProbeDbContext(connectionString, schoolA);
        var all = await context.Probes.IgnoreQueryFilters().ToListAsync();

        Assert.Equal(2, all.Count);
    }
}
