using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.Auth;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Infrastructure.Auth;
using SchoolMgmt.Infrastructure.Common;
using SchoolMgmt.Infrastructure.MultiTenancy;
using SchoolMgmt.Infrastructure.Persistence;
using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Students;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Application.FeeTemplates;
using SchoolMgmt.Application.Subjects;
using SchoolMgmt.Application.Teachers;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Application.TeacherAssignments;
using SchoolMgmt.Infrastructure.Persistence.Repositories;

namespace SchoolMgmt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.Configure<SeedDataOptions>(configuration.GetSection(SeedDataOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<StudentOptions>(configuration.GetSection(StudentOptions.SectionName));
        services.Configure<TeacherOptions>(configuration.GetSection(TeacherOptions.SectionName));
        services.Configure<InvoiceOptions>(configuration.GetSection(InvoiceOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpContextTenantProvider>(); // replaces StaticTenantProvider (specs/01) — see specs/02-implement-auth.md
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
        services.AddScoped<IGradeRepository, GradeRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IFeeTemplateRepository, FeeTemplateRepository>();
        services.AddScoped<IStudentSectionEnrollmentRepository, StudentSectionEnrollmentRepository>();
        services.AddScoped<ITeacherSectionSubjectRepository, TeacherSectionSubjectRepository>();
        services.AddScoped<IStudentFeeAssignmentRepository, StudentFeeAssignmentRepository>();
        services.AddScoped<IStudentDiscountAssignmentRepository, StudentDiscountAssignmentRepository>();
        services.AddScoped<IFeeInvoiceRepository, FeeInvoiceRepository>();

        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
