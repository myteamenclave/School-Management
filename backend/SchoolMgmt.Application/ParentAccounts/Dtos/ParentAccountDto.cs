namespace SchoolMgmt.Application.ParentAccounts.Dtos;

public record ParentAccountDto(
    Guid ParentUserId,
    string Email,
    string DisplayName,
    DateTimeOffset AccountCreatedAt
);
