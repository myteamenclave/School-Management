using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class FeeInvoiceInstallmentConfiguration : IEntityTypeConfiguration<FeeInvoiceInstallment>
{
    public void Configure(EntityTypeBuilder<FeeInvoiceInstallment> builder)
    {
        builder.ToTable("FeeInvoiceInstallments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Percentage).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
    }
}
