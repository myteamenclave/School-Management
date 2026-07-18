using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class SubjectTermGradeConfiguration : IEntityTypeConfiguration<SubjectTermGrade>
{
    public void Configure(EntityTypeBuilder<SubjectTermGrade> builder)
    {
        builder.ToTable("SubjectTermGrades");

        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.LetterGrade).HasMaxLength(4);
        builder.Property(e => e.MidtermScore).HasPrecision(5, 2);
        builder.Property(e => e.FinalScore).HasPrecision(5, 2);
        builder.Property(e => e.CourseworkScore).HasPrecision(5, 2);
        builder.Property(e => e.TermScore).HasPrecision(5, 2);

        // Section is NOT in the key — identity is student + subject + semester.
        builder.HasIndex(e => new { e.SchoolId, e.StudentId, e.SubjectId, e.SemesterId }).IsUnique();

        builder.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Subject).WithMany().HasForeignKey(e => e.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AcademicYear).WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Semester).WithMany().HasForeignKey(e => e.SemesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EnteredByUser).WithMany().HasForeignKey(e => e.EnteredByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
