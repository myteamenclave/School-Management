using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Infrastructure.Common;
using SchoolMgmt.Infrastructure.MultiTenancy;
using SchoolMgmt.Infrastructure.Persistence;
using SchoolMgmt.Infrastructure.Persistence.Repositories;

namespace SchoolMgmt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.Configure<SeedDataOptions>(configuration.GetSection(SeedDataOptions.SectionName));

        services.AddScoped<ITenantProvider, StaticTenantProvider>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
