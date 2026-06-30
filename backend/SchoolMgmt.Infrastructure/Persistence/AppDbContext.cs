using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantProvider tenantProvider,
    IDateTimeProvider dateTimeProvider)
    : DbContext(options)
{
    private readonly ITenantProvider _tenantProvider = tenantProvider;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public DbSet<School> Schools => Set<School>();

    private static readonly MethodInfo SetTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType)) continue;

            var method = SetTenantFilterMethod.MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.SchoolId == _tenantProvider.CurrentSchoolId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.SetCreated(now);
                if (entry.Entity is ITenantScoped tenantScoped && tenantScoped.SchoolId == Guid.Empty)
                    tenantScoped.SchoolId = _tenantProvider.CurrentSchoolId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.SetUpdated(now);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
