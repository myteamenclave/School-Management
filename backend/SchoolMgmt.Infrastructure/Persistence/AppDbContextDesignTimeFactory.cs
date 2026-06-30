using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Infrastructure.Common;
using SchoolMgmt.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Options;

namespace SchoolMgmt.Infrastructure.Persistence;

// EF Core CLI tooling auto-discovers this to construct AppDbContext at design
// time (migrations, scaffolding) without needing the WebApi host's DI/config.
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SCHOOLMGMT_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=schoolmgmt;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        var seedOptions = Options.Create(new SeedDataOptions());
        ITenantProvider tenantProvider = new StaticTenantProvider(seedOptions);
        IDateTimeProvider dateTimeProvider = new SystemDateTimeProvider();

        return new AppDbContext(optionsBuilder.Options, tenantProvider, dateTimeProvider);
    }
}
