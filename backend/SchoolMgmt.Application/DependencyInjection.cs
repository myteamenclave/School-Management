using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Auth;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Students;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Application.FeeTemplates;
using SchoolMgmt.Application.Subjects;
using SchoolMgmt.Application.Teachers;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Application.TeacherAssignments;
using SchoolMgmt.Application.Attendance;
using SchoolMgmt.Application.Gradebook;
using SchoolMgmt.Application.Dashboard;
using SchoolMgmt.Application.ParentAccounts;
using SchoolMgmt.Application.ParentPortal;

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
        services.AddScoped<SubjectService>();
        services.AddScoped<FeeTemplateService>();
        services.AddScoped<EnrollmentService>();
        services.AddScoped<TeacherAssignmentService>();
        services.AddScoped<FeeInvoiceService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<GradebookService>();
        services.AddScoped<GradeScaleService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ParentAccountService>();
        services.AddScoped<ParentPortalService>();
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
