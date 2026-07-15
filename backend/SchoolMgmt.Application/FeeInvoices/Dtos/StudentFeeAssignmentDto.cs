namespace SchoolMgmt.Application.FeeInvoices.Dtos;

public record StudentFeeAssignmentDto(
    Guid Id, Guid StudentId, string StudentName, string StudentCode,
    Guid FeeTemplateId, string TemplateName,
    Guid AcademicYearId, string AcademicYearName);
