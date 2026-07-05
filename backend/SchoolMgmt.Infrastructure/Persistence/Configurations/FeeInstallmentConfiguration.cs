using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class FeeInstallmentConfiguration : IEntityTypeConfiguration<FeeInstallment>
{
    public void Configure(EntityTypeBuilder<FeeInstallment> builder)
    {
        builder.ToTable("FeeInstallments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Percentage).IsRequired().HasColumnType("numeric(5,2)");
        builder.Property(x => x.DisplayOrder).IsRequired();
    }
}
