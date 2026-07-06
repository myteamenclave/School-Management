using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

internal sealed class TeacherSectionSubjectConfiguration : IEntityTypeConfiguration<TeacherSectionSubject>
{
    public void Configure(EntityTypeBuilder<TeacherSectionSubject> builder)
    {
        builder.ToTable("TeacherSectionSubjects");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SubjectId, x.SectionId, x.AcademicYearId }).IsUnique();

        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
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
