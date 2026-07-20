namespace SchoolMgmt.Application.ParentAccounts.Dtos;

/// <param name="AccountCreated">
/// true = a new Parent user was created with the supplied temporary password;
/// false = an existing Parent account was reused and its password was left untouched
/// (the supplied temporary password does NOT apply — the parent keeps their old one).
/// </param>
/// <param name="LinkCreated">
/// true = a new student↔parent link was created; false = the link already existed (no-op).
/// </param>
public record ParentLoginResultDto(
    Guid ParentUserId,
    string Email,
    string DisplayName,
    bool AccountCreated,
    bool LinkCreated
);
