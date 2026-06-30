using SchoolMgmt.Application.Interfaces;

namespace SchoolMgmt.Infrastructure.Tests.Auth.Fakes;

public class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"hash:{password}";

    public bool VerifyPassword(string passwordHash, string providedPassword) =>
        passwordHash == $"hash:{providedPassword}";
}
