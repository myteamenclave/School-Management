using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

internal sealed class StudentSectionEnrollmentConfiguration : IEntityTypeConfiguration<StudentSectionEnrollment>
{
    public void Configure(EntityTypeBuilder<StudentSectionEnrollment> builder)
    {
        builder.ToTable("StudentSectionEnrollments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
