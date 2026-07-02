using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);

        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(u => new { u.SchoolId, u.Email }).IsUnique();

        // No HasData seed here, deliberately — unlike School (specs/01), the demo
        // Admin user must never be seeded into a real production database.
        // Seeded at runtime instead, gated by IsDevelopment() — see DemoDataSeeder.
    }
}
