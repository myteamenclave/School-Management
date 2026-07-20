using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

internal sealed class StudentParentConfiguration : IEntityTypeConfiguration<StudentParent>
{
    public void Configure(EntityTypeBuilder<StudentParent> builder)
    {
        builder.ToTable("StudentParents");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.StudentId, x.UserId }).IsUnique();

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ParentUser)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
