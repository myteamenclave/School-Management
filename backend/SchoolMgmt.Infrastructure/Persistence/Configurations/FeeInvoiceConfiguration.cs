using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Persistence.Configurations;

public class FeeInvoiceConfiguration : IEntityTypeConfiguration<FeeInvoice>
{
    public void Configure(EntityTypeBuilder<FeeInvoice> builder)
    {
        builder.ToTable("FeeInvoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicYearId })
            .HasFilter("\"Status\" != 2")
            .IsUnique();

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FeeTemplate)
            .WithMany()
            .HasForeignKey(x => x.FeeTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LineItems)
            .WithOne(x => x.FeeInvoice)
            .HasForeignKey(x => x.FeeInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.LineItems).HasField("_lineItems")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Installments)
            .WithOne(x => x.FeeInvoice)
            .HasForeignKey(x => x.FeeInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Installments).HasField("_installments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
