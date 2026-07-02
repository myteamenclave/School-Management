namespace SchoolMgmt.Application.Auth;

public record LoginRequest(string Email, string Password, bool RememberMe = false);
