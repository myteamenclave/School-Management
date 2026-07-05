using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class FeeTemplateConfiguration : IEntityTypeConfiguration<FeeTemplate>
{
    public void Configure(EntityTypeBuilder<FeeTemplate> builder)
    {
        builder.ToTable("FeeTemplates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.GradeId, x.Name }).IsUnique();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Grade)
            .WithMany()
            .HasForeignKey(x => x.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LineItems)
            .WithOne(x => x.FeeTemplate)
            .HasForeignKey(x => x.FeeTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Installments)
            .WithOne(x => x.FeeTemplate)
            .HasForeignKey(x => x.FeeTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.DiscountRules)
            .WithOne(x => x.FeeTemplate)
            .HasForeignKey(x => x.FeeTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
