using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SchoolMgmt.IntegrationTests.Fakes;
using SchoolMgmt.IntegrationTests.TestSupport;
using Testcontainers.PostgreSql;
using AppDbContext = SchoolMgmt.Infrastructure.Persistence.AppDbContext;

namespace SchoolMgmt.IntegrationTests.Fixtures;

public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolmgmt_it")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply the real EF Core migrations (not EnsureCreated) so the seed
        // school is exercised through the actual deployment-equivalent path.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var context = new AppDbContext(
            options,
            new FakeTenantProvider(Guid.Empty),
            new FakeDateTimeProvider(DateTimeOffset.UtcNow));
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // A fresh, uniquely-named database on the same server — used by tests
    // that need ProbeEntity's table, which doesn't exist in the real
    // migration history. Returns the connection string so callers can open
    // multiple tenant-scoped contexts against the SAME probe database.
    public async Task<string> CreateProbeDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = $"probe_{Guid.NewGuid():N}"
        };
        var probeConnectionString = builder.ConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(probeConnectionString).Options;
        await using var context = new TestDbContext(
            options,
            new FakeTenantProvider(Guid.Empty),
            new FakeDateTimeProvider(DateTimeOffset.UtcNow));

        // Creates both the database and schema (incl. HasData seeds) from
        // the current model — this is test-only setup, not the real
        // migration path (that's covered separately by SeedDataTests).
        await context.Database.EnsureCreatedAsync();

        return probeConnectionString;
    }

    public static TestDbContext CreateProbeDbContext(string probeConnectionString, Guid tenantId, DateTimeOffset? now = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(probeConnectionString).Options;
        return new TestDbContext(
            options,
            new FakeTenantProvider(tenantId),
            new FakeDateTimeProvider(now ?? DateTimeOffset.UtcNow));
    }

    // Shared host factory for auth/HTTP-level integration tests — overrides
    // the connection string (points at this fixture's container) and the JWT
    // config (a fixed test secret, independent of appsettings.Development.json,
    // since WebApplicationFactory doesn't reliably load Development config).
    public WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // WebApplicationFactory defaults to "Production" if the ambient
            // ASPNETCORE_ENVIRONMENT isn't set — without this, DemoDataSeeder's
            // IsDevelopment() gate would skip seeding and every test that logs
            // in as the demo Admin would fail.
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = ConnectionString,
                    ["Jwt:Secret"] = "integration-test-secret-at-least-32-bytes-long!!",
                    ["Jwt:Issuer"] = "SchoolMgmt.IntegrationTests",
                    ["Jwt:Audience"] = "SchoolMgmt.IntegrationTests",
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"] = "7",
                });
            });
        });
}
