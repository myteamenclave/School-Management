using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Infrastructure.MultiTenancy;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class GradeScaleBandConfiguration : IEntityTypeConfiguration<GradeScaleBand>
{
    public void Configure(EntityTypeBuilder<GradeScaleBand> builder)
    {
        builder.ToTable("GradeScaleBands");
        builder.Property(e => e.Letter).HasMaxLength(4).IsRequired();
        builder.Property(e => e.MinScore).HasPrecision(5, 2);
        builder.Property(e => e.MaxScore).HasPrecision(5, 2);
        builder.HasIndex(e => new { e.SchoolId, e.Letter }).IsUnique();

        // Seed default bands for the seed school, same HasData-everywhere pattern as
        // SchoolConfiguration (specs/01). Anonymous objects (not GradeScaleBand instances)
        // so the private-set BaseEntity.CreatedAt can be given a static seed value via EF's
        // property-bag mapping. Static well-known Guids (no Guid.NewGuid() in HasData).
        var defaults = new SeedDataOptions();
        var seededAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            new { Id = Guid.Parse("00000000-0000-0000-0000-0000000000a1"), SchoolId = defaults.DefaultSchoolId, Letter = "A", MinScore = 90m, MaxScore = 100m,   CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null },
            new { Id = Guid.Parse("00000000-0000-0000-0000-0000000000a2"), SchoolId = defaults.DefaultSchoolId, Letter = "B", MinScore = 80m, MaxScore = 89.99m, CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null },
            new { Id = Guid.Parse("00000000-0000-0000-0000-0000000000a3"), SchoolId = defaults.DefaultSchoolId, Letter = "C", MinScore = 70m, MaxScore = 79.99m, CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null },
            new { Id = Guid.Parse("00000000-0000-0000-0000-0000000000a4"), SchoolId = defaults.DefaultSchoolId, Letter = "D", MinScore = 60m, MaxScore = 69.99m, CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null },
            new { Id = Guid.Parse("00000000-0000-0000-0000-0000000000a5"), SchoolId = defaults.DefaultSchoolId, Letter = "F", MinScore = 0m,  MaxScore = 59.99m, CreatedAt = seededAt, UpdatedAt = (DateTimeOffset?)null }
        );
    }
}
