using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Tests.Auth.Fakes;

public class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public string GenerateAccessToken(User user) => $"access-token-for-{user.Id}";

    public string GenerateRefreshToken() => $"refresh-token-{Guid.NewGuid()}";
}
