using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class StudentFeeAssignmentConfiguration : IEntityTypeConfiguration<StudentFeeAssignment>
{
    public void Configure(EntityTypeBuilder<StudentFeeAssignment> builder)
    {
        builder.ToTable("StudentFeeAssignments");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicYearId }).IsUnique();

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FeeTemplate)
            .WithMany()
            .HasForeignKey(x => x.FeeTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
