using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class DiscountRuleConfiguration : IEntityTypeConfiguration<DiscountRule>
{
    public void Configure(EntityTypeBuilder<DiscountRule> builder)
    {
        builder.ToTable("DiscountRules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RuleType).IsRequired().HasConversion<string>();
        builder.Property(x => x.Value).IsRequired().HasColumnType("numeric(18,2)");

        builder.HasOne(x => x.FeeLineItem)
            .WithMany()
            .HasForeignKey(x => x.FeeLineItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
