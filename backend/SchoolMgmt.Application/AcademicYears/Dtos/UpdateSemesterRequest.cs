namespace SchoolMgmt.Application.AcademicYears.Dtos;

public record UpdateSemesterRequest(string Name, DateOnly StartDate, DateOnly EndDate);
