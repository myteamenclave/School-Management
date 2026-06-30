using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.IntegrationTests.Fixtures;
using AppDbContext = SchoolMgmt.Infrastructure.Persistence.AppDbContext;

namespace SchoolMgmt.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class WebApiHostTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task CompositionRoot_ResolvesAppDbContextAndTenantProvider_AgainstARealHost()
    {
        await using var factory = fixture.CreateFactory();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

        // HttpContextTenantProvider.CurrentSchoolId is only resolvable on an
        // authenticated HTTP request (by design — see specs/02-implement-auth.md).
        // That path is covered by TenantResolutionTests. Here we only confirm DI
        // construction succeeds and the seeded data is reachable.
        Assert.NotNull(dbContext);
        Assert.NotNull(tenantProvider);
        var school = await dbContext.Schools.FindAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(school);
    }
}
