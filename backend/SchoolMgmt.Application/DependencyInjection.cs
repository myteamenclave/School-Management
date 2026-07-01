using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Auth;
using SchoolMgmt.Application.Grades;

namespace SchoolMgmt.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<AcademicYearService>();
        services.AddScoped<GradeService>();
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
