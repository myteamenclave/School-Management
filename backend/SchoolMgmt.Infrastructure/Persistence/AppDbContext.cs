using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

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
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<FeeTemplate> FeeTemplates => Set<FeeTemplate>();
    public DbSet<FeeLineItem> FeeLineItems => Set<FeeLineItem>();
    public DbSet<FeeInstallment> FeeInstallments => Set<FeeInstallment>();
    public DbSet<DiscountRule> DiscountRules => Set<DiscountRule>();
    public DbSet<StudentSectionEnrollment> StudentSectionEnrollments => Set<StudentSectionEnrollment>();
    public DbSet<StudentParent> StudentParents => Set<StudentParent>();
    public DbSet<TeacherSectionSubject> TeacherSectionSubjects => Set<TeacherSectionSubject>();
    public DbSet<StudentFeeAssignment> StudentFeeAssignments => Set<StudentFeeAssignment>();
    public DbSet<StudentDiscountAssignment> StudentDiscountAssignments => Set<StudentDiscountAssignment>();
    public DbSet<FeeInvoice> FeeInvoices => Set<FeeInvoice>();
    public DbSet<FeeInvoiceLineItem> FeeInvoiceLineItems => Set<FeeInvoiceLineItem>();
    public DbSet<FeeInvoiceInstallment> FeeInvoiceInstallments => Set<FeeInvoiceInstallment>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<SubjectTermGrade> SubjectTermGrades => Set<SubjectTermGrade>();
    public DbSet<GradeScaleBand> GradeScaleBands => Set<GradeScaleBand>();
    public DbSet<Payment> Payments => Set<Payment>();

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
