using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Gradebook;

[Collection(IntegrationTestCollection.Name)]
public class GradeScaleControllerTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static HttpRequestMessage Get(string url, string cookies) =>
        new HttpRequestMessage(HttpMethod.Get, url).WithCookies(cookies);

    private static HttpRequestMessage Post(string url, string cookies, object body)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, url).WithCookies(cookies);
        msg.Content = JsonContent.Create(body);
        return msg;
    }

    private static HttpRequestMessage Put(string url, string cookies, object body)
    {
        var msg = new HttpRequestMessage(HttpMethod.Put, url).WithCookies(cookies);
        msg.Content = JsonContent.Create(body);
        return msg;
    }

    private static HttpRequestMessage Delete(string url, string cookies) =>
        new HttpRequestMessage(HttpMethod.Delete, url).WithCookies(cookies);

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return CookieTestHelpers.BuildCookieHeader(res);
    }

    private static Task<string> LoginAsAdminAsync(HttpClient client) =>
        LoginAsync(client, "admin@demoschool.test", "Passw0rd!");

    // A band letter unique to this test run (3 hex chars, distinct from seeded single-letter A–F).
    private static string UniqueLetter(string tag) => tag[..3].ToUpperInvariant();

    private static async Task<string> LoginAsTeacherAsync(HttpClient client, string adminCookies, string tag)
    {
        var res = await client.SendAsync(Post("/api/teachers", adminCookies, new
        {
            email = $"teacher-gs-{tag}@demoschool.test",
            password = "Passw0rd!",
            firstName = "Test",
            lastName = "Teacher",
            phone = "555-0200",
            joiningDate = "2025-09-01",
        }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await LoginAsync(client, $"teacher-gs-{tag}@demoschool.test", "Passw0rd!");
    }

    [Fact]
    public async Task GetAll_AsAdmin_ReturnsSeededBands()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        var bands = await (await client.SendAsync(Get("/api/grade-scale", admin)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Seeded A–F for the seed school; ordered MinScore desc so the first is the top band.
        Assert.True(bands.GetArrayLength() >= 5);
        var letters = bands.EnumerateArray().Select(b => b.GetProperty("letter").GetString()).ToList();
        Assert.Contains("A", letters);
        Assert.Contains("F", letters);
    }

    [Fact]
    public async Task Create_AsAdmin_Returns200_AndAppearsInList()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var letter = UniqueLetter(tag);

        var create = await client.SendAsync(Post("/api/grade-scale", admin,
            new { letter, minScore = 33m, maxScore = 44m }));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(letter, created.GetProperty("letter").GetString());

        var bands = await (await client.SendAsync(Get("/api/grade-scale", admin)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Contains(bands.EnumerateArray(), b => b.GetProperty("letter").GetString() == letter);
    }

    [Fact]
    public async Task Create_MinGreaterThanMax_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var res = await client.SendAsync(Post("/api/grade-scale", admin,
            new { letter = UniqueLetter(tag), minScore = 80m, maxScore = 20m }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesValues()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var letter = UniqueLetter(tag);

        var created = await (await client.SendAsync(Post("/api/grade-scale", admin,
            new { letter, minScore = 33m, maxScore = 44m }))).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var update = await client.SendAsync(Put($"/api/grade-scale/{id}", admin,
            new { letter, minScore = 30m, maxScore = 39m }));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(30m, updated.GetProperty("minScore").GetDecimal());
        Assert.Equal(39m, updated.GetProperty("maxScore").GetDecimal());
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        var res = await client.SendAsync(Put($"/api/grade-scale/{Guid.NewGuid()}", admin,
            new { letter = "ZZ", minScore = 10m, maxScore = 20m }));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesBand()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var letter = UniqueLetter(tag);

        var created = await (await client.SendAsync(Post("/api/grade-scale", admin,
            new { letter, minScore = 33m, maxScore = 44m }))).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var del = await client.SendAsync(Delete($"/api/grade-scale/{id}", admin));
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var bands = await (await client.SendAsync(Get("/api/grade-scale", admin)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.DoesNotContain(bands.EnumerateArray(), b => b.GetProperty("letter").GetString() == letter);
    }

    // Teachers may READ the grade scale (the gradebook maps term scores to letters
    // live). Writes remain Admin-only — see Create/Update/Delete_AsTeacher below.
    [Fact]
    public async Task GetAll_AsTeacher_Returns200()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var teacher = await LoginAsTeacherAsync(client, admin, tag);

        var res = await client.SendAsync(Get("/api/grade-scale", teacher));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Create_AsTeacher_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var teacher = await LoginAsTeacherAsync(client, admin, tag);

        var res = await client.SendAsync(Post("/api/grade-scale", teacher,
            new { letter = UniqueLetter(tag), minScore = 10m, maxScore = 20m }));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync("/api/grade-scale");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
