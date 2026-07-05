using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class FeeLineItemConfiguration : IEntityTypeConfiguration<FeeLineItem>
{
    public void Configure(EntityTypeBuilder<FeeLineItem> builder)
    {
        builder.ToTable("FeeLineItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Amount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(x => x.DisplayOrder).IsRequired();
    }
}
