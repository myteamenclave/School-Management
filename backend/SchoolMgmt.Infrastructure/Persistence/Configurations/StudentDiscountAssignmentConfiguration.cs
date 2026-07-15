using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class StudentDiscountAssignmentConfiguration : IEntityTypeConfiguration<StudentDiscountAssignment>
{
    public void Configure(EntityTypeBuilder<StudentDiscountAssignment> builder)
    {
        builder.ToTable("StudentDiscountAssignments");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.DiscountRuleId, x.AcademicYearId }).IsUnique();

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DiscountRule)
            .WithMany()
            .HasForeignKey(x => x.DiscountRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
