using System.Net;
using System.Net.Http.Json;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public class TenantResolutionTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task Me_OnAuthenticatedRequest_ResolvesSchoolId_ViaHttpContextTenantProvider()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@demoschool.test", password = "Passw0rd!" });
        var cookies = CookieTestHelpers.BuildCookieHeader(loginResponse);

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me").WithCookies(cookies);
        var meResponse = await client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var body = await meResponse.Content.ReadFromJsonAsync<MeResponse>();

        Assert.NotNull(body);
        // The well-known seeded school id (specs/01) — proves HttpContextTenantProvider
        // resolved SchoolId from the JWT claim on a real request, replacing
        // StaticTenantProvider with zero other code changes (specs/01's payoff).
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), body!.SchoolId);
    }

    [Fact]
    public async Task Me_WithoutAuthentication_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record MeResponse(string Id, string Email, string DisplayName, string Role, Guid SchoolId);
}
