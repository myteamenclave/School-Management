using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Infrastructure.Persistence;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.ParentAccounts;

[Collection(IntegrationTestCollection.Name)]
public class ParentLinkTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string DemoSchoolId = "00000000-0000-0000-0000-000000000001";

    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@demoschool.test", password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return CookieTestHelpers.BuildCookieHeader(res);
    }

    private static HttpRequestMessage Get(string url, string cookies) =>
        new HttpRequestMessage(HttpMethod.Get, url).WithCookies(cookies);

    private static HttpRequestMessage Post(string url, string cookies, object body)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, url).WithCookies(cookies);
        msg.Content = JsonContent.Create(body);
        return msg;
    }

    private static HttpRequestMessage Delete(string url, string cookies) =>
        new HttpRequestMessage(HttpMethod.Delete, url).WithCookies(cookies);

    private static string Uniq() => Guid.NewGuid().ToString("N")[..8];

    // Seed a student via API, optionally with a guardian email. Returns studentId.
    private static async Task<string> SeedStudentAsync(HttpClient client, string cookies,
        string firstName, string lastName, string? guardianEmail, string? guardianName = "Guardian Name")
    {
        var res = await client.SendAsync(Post("/api/students", cookies, new
        {
            firstName,
            lastName,
            dateOfBirth = "2010-03-15",
            gender = "Male",
            enrollmentDate = "2025-09-01",
            guardianName,
            guardianEmail,
        }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task CreateParentLogin_ValidStudent_Returns200_CreatesParentUser()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var studentId = await SeedStudentAsync(client, cookies, "Alice", "Smith", email);

        var res = await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(body.GetProperty("accountCreated").GetBoolean());
        Assert.True(body.GetProperty("linkCreated").GetBoolean());
        Assert.Equal(email, body.GetProperty("email").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == email);
        Assert.Equal(UserRole.Parent, user.Role);
    }

    [Fact]
    public async Task CreatedParent_CanLogIn_WithTemporaryPassword()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var studentId = await SeedStudentAsync(client, cookies, "Bob", "Jones", email);

        await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "Sup3rSecret!" }));

        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Sup3rSecret!" });

        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
    }

    [Fact]
    public async Task CreateParentLogin_SameEmailForTwoChildren_ReusesOneParentAccount()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var childA = await SeedStudentAsync(client, cookies, "Child", "Aaa", email);
        var childB = await SeedStudentAsync(client, cookies, "Child", "Bbb", email);

        var resA = await client.SendAsync(Post($"/api/students/{childA}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));
        var resB = await client.SendAsync(Post($"/api/students/{childB}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));

        var bodyA = await resA.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var bodyB = await resB.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.True(bodyA.GetProperty("accountCreated").GetBoolean());
        Assert.False(bodyB.GetProperty("accountCreated").GetBoolean());   // reused
        Assert.True(bodyB.GetProperty("linkCreated").GetBoolean());
        Assert.Equal(
            bodyA.GetProperty("parentUserId").GetString(),
            bodyB.GetProperty("parentUserId").GetString());               // same account
    }

    [Fact]
    public async Task CreateParentLogin_CalledTwiceForSameStudent_IsIdempotent()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var studentId = await SeedStudentAsync(client, cookies, "Carol", "Lee", email);

        await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));
        var second = await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));

        var body = await second.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(body.GetProperty("accountCreated").GetBoolean());
        Assert.False(body.GetProperty("linkCreated").GetBoolean());

        var list = await (await client.SendAsync(Get($"/api/students/{studentId}/parents", cookies)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task CreateParentLogin_BlankGuardianEmail_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var studentId = await SeedStudentAsync(client, cookies, "Dave", "Brown", guardianEmail: null);

        var res = await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreateParentLogin_EmailOwnedByNonParent_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"teacher-{Uniq()}@demoschool.test";

        // Seed a Teacher user directly with that email.
        using (var scope = factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                SchoolId = Guid.Parse(DemoSchoolId),
                Email = email,
                PasswordHash = hasher.HashPassword("TeacherPass1!"),
                DisplayName = "A Teacher",
                Role = UserRole.Teacher,
            });
            await db.SaveChangesAsync();
        }

        var studentId = await SeedStudentAsync(client, cookies, "Erin", "Fox", email);

        var res = await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task CreateParentLogin_ShortPassword_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var studentId = await SeedStudentAsync(client, cookies, "Fay", "Green", email);

        var res = await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "short" }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetParents_ReturnsLinkedParent()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var studentId = await SeedStudentAsync(client, cookies, "Gia", "Hall", email);
        await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));

        var res = await client.SendAsync(Get($"/api/students/{studentId}/parents", cookies));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(email, body[0].GetProperty("email").GetString());
        Assert.False(string.IsNullOrEmpty(body[0].GetProperty("displayName").GetString()));
    }

    [Fact]
    public async Task GetParents_UnknownStudent_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var res = await client.SendAsync(Get($"/api/students/{Guid.NewGuid()}/parents", cookies));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_Returns204_RemovesLinkButKeepsUser()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var studentId = await SeedStudentAsync(client, cookies, "Hana", "Ivy", email);
        var create = await client.SendAsync(Post($"/api/students/{studentId}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));
        var parentUserId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("parentUserId").GetString()!;

        var del = await client.SendAsync(Delete($"/api/students/{studentId}/parents/{parentUserId}", cookies));
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var list = await (await client.SendAsync(Get($"/api/students/{studentId}/parents", cookies)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, list.GetArrayLength());

        // The Parent user row still exists.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email));
    }

    [Fact]
    public async Task DeleteLink_UnknownLink_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var studentId = await SeedStudentAsync(client, cookies, "Ida", "Jang", email);

        var res = await client.SendAsync(Delete($"/api/students/{studentId}/parents/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task ParentLinkedToTwoChildren_DeleteOneLink_StillLinkedToOther()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var childA = await SeedStudentAsync(client, cookies, "Child", "Aaa", email);
        var childB = await SeedStudentAsync(client, cookies, "Child", "Bbb", email);

        var createA = await client.SendAsync(Post($"/api/students/{childA}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));
        var parentUserId = (await createA.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("parentUserId").GetString()!;
        await client.SendAsync(Post($"/api/students/{childB}/parent-login", cookies,
            new { temporaryPassword = "Passw0rd!" }));

        await client.SendAsync(Delete($"/api/students/{childA}/parents/{parentUserId}", cookies));

        var listB = await (await client.SendAsync(Get($"/api/students/{childB}/parents", cookies)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, listB.GetArrayLength());
        Assert.Equal(parentUserId, listB[0].GetProperty("parentUserId").GetString());
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync($"/api/students/{Guid.NewGuid()}/parents");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task TeacherRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var teacherEmail = $"teacher-pp-{Uniq()}@demoschool.test";
        const string teacherPassword = "TeacherPass1!";

        using (var scope = factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                SchoolId = Guid.Parse(DemoSchoolId),
                Email = teacherEmail,
                PasswordHash = hasher.HashPassword(teacherPassword),
                DisplayName = "Teacher PP",
                Role = UserRole.Teacher,
            });
            await db.SaveChangesAsync();
        }

        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginRes);

        var res = await client.SendAsync(Get($"/api/students/{Guid.NewGuid()}/parents", teacherCookies));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
