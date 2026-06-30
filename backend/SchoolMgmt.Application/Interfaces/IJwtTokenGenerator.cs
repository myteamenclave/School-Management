using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken(); // raw, high-entropy random string — not a JWT
}
