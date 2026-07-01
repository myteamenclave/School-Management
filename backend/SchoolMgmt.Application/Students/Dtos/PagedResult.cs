namespace SchoolMgmt.Application.Students.Dtos;

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
