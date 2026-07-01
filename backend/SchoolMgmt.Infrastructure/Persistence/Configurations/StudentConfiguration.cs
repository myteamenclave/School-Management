using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentCode).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => new { x.SchoolId, x.StudentCode }).IsUnique();

        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DateOfBirth).IsRequired().HasColumnType("date");
        builder.Property(x => x.Gender).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.EnrollmentDate).IsRequired().HasColumnType("date");
        builder.Property(x => x.EnrollmentStatus).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(x => x.GuardianName).HasMaxLength(200);
        builder.Property(x => x.GuardianPhone).HasMaxLength(20);
        builder.Property(x => x.GuardianEmail).HasMaxLength(256);
    }
}
