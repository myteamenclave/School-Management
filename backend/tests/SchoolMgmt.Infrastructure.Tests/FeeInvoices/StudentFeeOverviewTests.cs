using System.Reflection;
using Microsoft.Extensions.Options;
using SchoolMgmt.Application.FeeInvoices;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;
using SchoolMgmt.Infrastructure.Tests.Fakes;
using SchoolMgmt.Infrastructure.Tests.FeeInvoices.Fakes;

namespace SchoolMgmt.Infrastructure.Tests.FeeInvoices;

// The fee balance rollup is a business rule (spec 20): Billed/Paid/Outstanding/Next-Due plus
// on-the-fly overdue (unpaid AND DueDate < today). These isolate that math — one authoritative
// server-side definition the parent portal (and future pay-online/dashboard) reuse.
public class StudentFeeOverviewTests
{
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid YearId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 1, 15);

    // GetStudentFeeOverviewAsync touches only invoiceRepo + dateTimeProvider; the rest is null!.
    // Positional args mirror the ctor: assignment, discount, invoiceRepo, lineItem, installment,
    // template, year, grade, student, enrollment, paymentRepo, unitOfWork, dateTimeProvider, options.
    private static FeeInvoiceService BuildService(FakeFeeInvoiceRepository repo) =>
        new(null!, null!, repo, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new FakeDateTimeProvider(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)),
            Options.Create(new InvoiceOptions()));

    // Reflection seeds the encapsulated backing collections (EF fills these via HasField in prod).
    private static void AddToBackingList<T>(FeeInvoice invoice, string field, T item)
    {
        var f = typeof(FeeInvoice).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((List<T>)f.GetValue(invoice)!).Add(item);
    }

    private static FeeInvoice IssuedInvoice(
        decimal total, params FeeInvoiceInstallment[] installments)
    {
        var invoice = new FeeInvoice
        {
            StudentId = StudentId,
            AcademicYearId = YearId,
            Status = InvoiceStatus.Issued,
            TotalAmount = total,
            InvoiceCode = "INV-2026-000001",
            Student = new Student { FirstName = "Test", LastName = "Kid", StudentCode = "2026-000001" },
            AcademicYear = new AcademicYear { Name = "2025-2026" },
            FeeTemplate = new FeeTemplate { Name = "Standard" },
        };
        foreach (var i in installments) AddToBackingList(invoice, "_installments", i);
        return invoice;
    }

    private static FeeInvoiceInstallment Installment(
        decimal amount, DateOnly? dueDate, decimal? amountPaid = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Installment",
        Amount = amount,
        DueDate = dueDate,
        AmountPaid = amountPaid,
        Status = InstallmentStatus.Pending,
    };

    [Fact]
    public async Task NoIssuedInvoice_ReturnsEmptySummaryAndNullInvoice()
    {
        var service = BuildService(new FakeFeeInvoiceRepository());

        var result = await service.GetStudentFeeOverviewAsync(StudentId, YearId);

        Assert.False(result.Summary.HasInvoice);
        Assert.Equal(0m, result.Summary.TotalBilled);
        Assert.Equal(0m, result.Summary.Outstanding);
        Assert.Equal(0, result.Summary.OverdueCount);
        Assert.Empty(result.Summary.OverdueInstallmentIds);
        Assert.Null(result.Invoice);
    }

    [Fact]
    public async Task Issued_NothingPaid_FutureDueDates_NoOverdue()
    {
        var repo = new FakeFeeInvoiceRepository();
        var i1 = Installment(600m, Today.AddMonths(1));
        var i2 = Installment(400m, Today.AddMonths(2));
        repo.Seed(IssuedInvoice(1000m, i1, i2));
        var service = BuildService(repo);

        var s = (await service.GetStudentFeeOverviewAsync(StudentId, YearId)).Summary;

        Assert.True(s.HasInvoice);
        Assert.Equal(1000m, s.TotalBilled);
        Assert.Equal(0m, s.TotalPaid);
        Assert.Equal(1000m, s.Outstanding);
        Assert.Equal(0, s.OverdueCount);
        Assert.Equal(i1.DueDate, s.NextDueDate);   // earliest unpaid
        Assert.Equal(600m, s.NextDueAmount);
    }

    [Fact]
    public async Task Issued_PastDueUnpaid_CountsAsOverdue()
    {
        var repo = new FakeFeeInvoiceRepository();
        var overdue = Installment(600m, Today.AddMonths(-1));   // past due, unpaid
        var future = Installment(400m, Today.AddMonths(1));
        repo.Seed(IssuedInvoice(1000m, overdue, future));
        var service = BuildService(repo);

        var s = (await service.GetStudentFeeOverviewAsync(StudentId, YearId)).Summary;

        Assert.Equal(1, s.OverdueCount);
        Assert.Equal(600m, s.OverdueAmount);
        Assert.Contains(overdue.Id, s.OverdueInstallmentIds);
        Assert.DoesNotContain(future.Id, s.OverdueInstallmentIds);
        Assert.Equal(overdue.DueDate, s.NextDueDate);   // earliest unpaid = the overdue one
    }

    [Fact]
    public async Task PartiallyPaidInstallment_UsesRemainingForOutstandingAndNextDue()
    {
        var repo = new FakeFeeInvoiceRepository();
        var partial = Installment(600m, Today.AddMonths(1), amountPaid: 250m);
        repo.Seed(IssuedInvoice(600m, partial));
        var service = BuildService(repo);

        var s = (await service.GetStudentFeeOverviewAsync(StudentId, YearId)).Summary;

        Assert.Equal(250m, s.TotalPaid);
        Assert.Equal(350m, s.Outstanding);
        Assert.Equal(350m, s.NextDueAmount);   // remaining, not the full amount
    }

    [Fact]
    public async Task FullyPaidInstallment_ExcludedFromOverdueAndNextDue()
    {
        var repo = new FakeFeeInvoiceRepository();
        var paid = Installment(600m, Today.AddMonths(-1), amountPaid: 600m);   // past due but fully paid
        repo.Seed(IssuedInvoice(600m, paid));
        var service = BuildService(repo);

        var s = (await service.GetStudentFeeOverviewAsync(StudentId, YearId)).Summary;

        Assert.Equal(600m, s.TotalPaid);
        Assert.Equal(0m, s.Outstanding);
        Assert.Equal(0, s.OverdueCount);
        Assert.Null(s.NextDueDate);
        Assert.Null(s.NextDueAmount);
    }

    [Fact]
    public async Task InstallmentWithoutDueDate_NeverOverdue_ExcludedFromNextDue()
    {
        var repo = new FakeFeeInvoiceRepository();
        var noDate = Installment(600m, dueDate: null);
        repo.Seed(IssuedInvoice(600m, noDate));
        var service = BuildService(repo);

        var s = (await service.GetStudentFeeOverviewAsync(StudentId, YearId)).Summary;

        Assert.Equal(0, s.OverdueCount);
        Assert.Null(s.NextDueDate);
    }
}
