using System.Net;
using System.Net.Http.Json;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public class RefreshRotationTests(PostgresContainerFixture fixture)
{
    private static async Task<string> LoginAndGetCookiesAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@demoschool.test", password = "Passw0rd!" });
        return CookieTestHelpers.BuildCookieHeader(response);
    }

    [Fact]
    public async Task Refresh_WithValidCookie_RotatesAndIssuesNewCookies_OldTokenNoLongerWorks()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var originalCookies = await LoginAndGetCookiesAsync(client);

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh").WithCookies(originalCookies);
        var refreshResponse = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var newCookies = CookieTestHelpers.BuildCookieHeader(refreshResponse);
        Assert.NotEqual(originalCookies, newCookies);

        // The OLD refresh token cookie must no longer work.
        var reuseOldRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh").WithCookies(originalCookies);
        var reuseOldResponse = await client.SendAsync(reuseOldRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseOldResponse.StatusCode);
    }

    [Fact]
    public async Task ReplayingAlreadyRotatedToken_RevokesWholeSessionFamily_IncludingTheNewestToken()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var originalCookies = await LoginAndGetCookiesAsync(client);

        var firstRefresh = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh").WithCookies(originalCookies));
        var newestCookies = CookieTestHelpers.BuildCookieHeader(firstRefresh);

        // Replay the OLD (already-rotated) token — theft signal.
        var replay = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh").WithCookies(originalCookies));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The newest (legitimately rotated) token must ALSO now be rejected — whole family revoked.
        var attemptWithNewest = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh").WithCookies(newestCookies));
        Assert.Equal(HttpStatusCode.Unauthorized, attemptWithNewest.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutACookie_ReturnsUnauthorized()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
