using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Infrastructure.MultiTenancy;

namespace SchoolMgmt.Infrastructure.Persistence;

// Demo Admin user must never exist in a real deployment — seeded at runtime,
// gated by IsDevelopment(), rather than via migration HasData (which has no
// concept of environment at apply-time). Unlike School (specs/01), which is
// fine to seed everywhere via HasData.
public static class DemoDataSeeder
{
    public static async Task SeedDemoDataAsync(this IServiceProvider services, IHostEnvironment environment, CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment()) return;

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedDataOptions>>().Value;

        // IgnoreQueryFilters() — runs outside any authenticated request, so
        // ITenantProvider.CurrentSchoolId isn't resolvable. Same documented
        // exception as the pre-auth repository lookups (specs/02-implement-auth.md).
        var alreadySeeded = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == seedOptions.DefaultAdminEmail, cancellationToken);
        if (alreadySeeded) return;

        context.Users.Add(new User
        {
            SchoolId = seedOptions.DefaultSchoolId, // set explicitly — same reason as AuthService: no resolvable tenant here
            Email = seedOptions.DefaultAdminEmail,
            PasswordHash = seedOptions.DefaultAdminPasswordHash,
            DisplayName = seedOptions.DefaultAdminDisplayName,
            Role = UserRole.Admin,
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
