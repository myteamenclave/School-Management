using System.Net;
using System.Net.Http.Json;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public class LogoutTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task Logout_RevokesRefreshTokenServerSide_SubsequentRefreshFails()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@demoschool.test", password = "Passw0rd!" });
        var cookies = CookieTestHelpers.BuildCookieHeader(loginResponse);

        var logoutResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout").WithCookies(cookies));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshAfterLogout = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh").WithCookies(cookies));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_WithNoCookie_IsIdempotent_DoesNotError()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
