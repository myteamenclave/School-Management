using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SchoolMgmt.Application.Auth;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Infrastructure.Auth;
using SchoolMgmt.Infrastructure.Tests.Fakes;

namespace SchoolMgmt.Infrastructure.Tests.Auth;

public class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateAccessToken_ProducesTokenWithExpectedClaims()
    {
        var now = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var options = Options.Create(new JwtOptions
        {
            Secret = "unit-test-secret-at-least-32-bytes-long!!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenMinutes = 15,
        });
        var generator = new JwtTokenGenerator(options, new FakeDateTimeProvider(now));

        var user = new User
        {
            Email = "admin@demoschool.test",
            DisplayName = "Demo Admin",
            Role = UserRole.Admin,
            SchoolId = Guid.NewGuid(),
        };

        var token = generator.GenerateAccessToken(user);
        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), decoded.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.SchoolId.ToString(), decoded.Claims.Single(c => c.Type == "school_id").Value);
        Assert.Equal("Admin", decoded.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(user.Email, decoded.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("TestIssuer", decoded.Issuer);
        Assert.Equal(now.AddMinutes(15).UtcDateTime, decoded.ValidTo);
    }

    [Fact]
    public void GenerateRefreshToken_ProducesHighEntropyUniqueValues()
    {
        var options = Options.Create(new JwtOptions { Secret = "unit-test-secret-at-least-32-bytes-long!!" });
        var generator = new JwtTokenGenerator(options, new FakeDateTimeProvider(DateTimeOffset.UtcNow));

        var a = generator.GenerateRefreshToken();
        var b = generator.GenerateRefreshToken();

        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 32); // base64url of 32 random bytes
    }
}
