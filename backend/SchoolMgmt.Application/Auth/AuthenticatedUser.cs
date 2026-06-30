using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Auth;

public record AuthenticatedUser(Guid Id, string Email, string DisplayName, UserRole Role);
