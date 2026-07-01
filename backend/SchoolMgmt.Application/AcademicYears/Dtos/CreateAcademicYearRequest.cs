namespace SchoolMgmt.Application.AcademicYears.Dtos;

public record CreateAcademicYearRequest(string Name, DateOnly StartDate, DateOnly EndDate);
