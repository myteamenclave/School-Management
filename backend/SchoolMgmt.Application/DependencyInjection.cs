using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.Auth;

namespace SchoolMgmt.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();

        return services;
    }
}
