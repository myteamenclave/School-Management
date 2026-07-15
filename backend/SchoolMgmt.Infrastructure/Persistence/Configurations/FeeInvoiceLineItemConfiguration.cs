using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class FeeInvoiceLineItemConfiguration : IEntityTypeConfiguration<FeeInvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<FeeInvoiceLineItem> builder)
    {
        builder.ToTable("FeeInvoiceLineItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.OriginalAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.FinalAmount).HasPrecision(18, 2);
    }
}
