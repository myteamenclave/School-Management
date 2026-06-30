using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Infrastructure.MultiTenancy;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);

        // Seed values must be static — defaults match SeedDataOptions so the
        // migration-time seed agrees with the demo school id used at runtime.
        // Anonymous object (not a School instance) so CreatedAt — private-set
        // on BaseEntity — can still be given a concrete seed value via EF's
        // property-bag mapping, without widening BaseEntity's encapsulation.
        var defaults = new SeedDataOptions();
        builder.HasData(new
        {
            Id = defaults.DefaultSchoolId,
            Name = defaults.DefaultSchoolName,
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = (DateTimeOffset?)null,
        });
    }
}
