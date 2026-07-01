using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

internal class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => new { x.GradeId, x.Name }).IsUnique();

        builder.HasOne(x => x.Grade)
            .WithMany(g => g.Sections)
            .HasForeignKey(x => x.GradeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
