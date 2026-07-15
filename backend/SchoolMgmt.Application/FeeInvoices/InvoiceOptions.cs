namespace SchoolMgmt.Application.FeeInvoices;

public class InvoiceOptions
{
    public const string SectionName = "Invoices";
    public int InvoiceCodeMaxRetries { get; set; } = 3;
}
