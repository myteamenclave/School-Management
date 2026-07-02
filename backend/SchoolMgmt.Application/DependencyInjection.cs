using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Auth;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Students;
using SchoolMgmt.Application.Teachers;

namespace SchoolMgmt.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<AcademicYearService>();
        services.AddScoped<GradeService>();
        services.AddScoped<StudentService>();
        services.AddScoped<TeacherService>();
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
