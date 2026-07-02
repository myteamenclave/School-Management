using System.Net;
using System.Net.Http.Json;
using SchoolMgmt.IntegrationTests.Fixtures;

namespace SchoolMgmt.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public class LoginTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task Login_WithSeededDemoCredentials_ReturnsCorrectlyAttributedCookies()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@demoschool.test", password = "Passw0rd!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Equal(2, setCookies.Count);

        var accessCookie = setCookies.Single(c => c.StartsWith("access_token="));
        var refreshCookie = setCookies.Single(c => c.StartsWith("refresh_token="));

        foreach (var cookie in new[] { accessCookie, refreshCookie })
        {
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsGenericUnauthorized()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@demoschool.test", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ReturnsSameUnauthorizedAsWrongPassword()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@demoschool.test", password = "anything" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithRememberMeTrue_SetsCookiesWithExpires()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@demoschool.test", password = "Passw0rd!", rememberMe = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.All(setCookies, c => Assert.Contains("expires=", c, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_WithRememberMeFalse_SetsCookiesWithoutExpires()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@demoschool.test", password = "Passw0rd!", rememberMe = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.All(setCookies, c => Assert.DoesNotContain("expires=", c, StringComparison.OrdinalIgnoreCase));
    }
}
