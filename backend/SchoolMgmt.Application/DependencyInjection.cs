using Microsoft.Extensions.DependencyInjection;

namespace SchoolMgmt.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // No Application-layer services yet — feature specs register their
        // own services here as they're implemented.
        return services;
    }
}
