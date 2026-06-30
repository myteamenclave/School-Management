using Microsoft.EntityFrameworkCore;
using SchoolMgmt.IntegrationTests.Fakes;
using SchoolMgmt.IntegrationTests.Fixtures;
using AppDbContext = SchoolMgmt.Infrastructure.Persistence.AppDbContext;

namespace SchoolMgmt.IntegrationTests.Persistence;

[Collection(IntegrationTestCollection.Name)]
public class SeedDataTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task InitialMigration_SeedsExactlyOneSchool_WithTheWellKnownId()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var context = new AppDbContext(
            options,
            new FakeTenantProvider(Guid.Empty),
            new FakeDateTimeProvider(DateTimeOffset.UtcNow));

        var schools = await context.Schools.ToListAsync();

        var school = Assert.Single(schools);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), school.Id);
        Assert.Equal("Demo School", school.Name);
    }
}
