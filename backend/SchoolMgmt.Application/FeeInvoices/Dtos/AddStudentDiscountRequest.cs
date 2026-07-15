namespace SchoolMgmt.Application.FeeInvoices.Dtos;

public record AddStudentDiscountRequest(Guid DiscountRuleId, Guid AcademicYearId);
