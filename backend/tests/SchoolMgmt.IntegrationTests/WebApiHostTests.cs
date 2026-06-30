using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = fixture.ConnectionString
                });
            });
        });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

        // StaticTenantProvider's CurrentSchoolId is the same well-known id seeded by the migration.
        var school = await dbContext.Schools.FindAsync(tenantProvider.CurrentSchoolId);

        Assert.NotNull(dbContext);
        Assert.NotEqual(Guid.Empty, tenantProvider.CurrentSchoolId);
        Assert.NotNull(school);
    }
}
