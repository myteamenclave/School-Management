namespace SchoolMgmt.Application.FeeInvoices.Dtos;

public record GenerateInvoicesRequest(
    Guid GradeId,
    Guid AcademicYearId,
    List<InstallmentDueDateInput> InstallmentDueDates);

public record InstallmentDueDateInput(Guid TemplateInstallmentId, DateOnly DueDate);
