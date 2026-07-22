using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.StripePaymentIntentId).IsRequired().HasMaxLength(255);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        // The idempotency backstop at the DB — one Payment per Stripe intent.
        builder.HasIndex(x => x.StripePaymentIntentId).IsUnique();

        builder.HasOne(x => x.FeeInvoiceInstallment)
            .WithMany()
            .HasForeignKey(x => x.FeeInvoiceInstallmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
