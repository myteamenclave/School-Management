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

namespace SchoolMgmt.IntegrationTests.Subjects;

[Collection(IntegrationTestCollection.Name)]
public class SubjectsControllerTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string UniqueCode(string prefix = "SUB") =>
        $"{prefix}{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@demoschool.test", password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return CookieTestHelpers.BuildCookieHeader(loginResponse);
    }

    private static HttpRequestMessage Get(string url, string cookies) =>
        new HttpRequestMessage(HttpMethod.Get, url).WithCookies(cookies);

    private static HttpRequestMessage Post(string url, string cookies, object body)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, url).WithCookies(cookies);
        msg.Content = JsonContent.Create(body);
        return msg;
    }

    private static HttpRequestMessage Put(string url, string cookies, object body) =>
        new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) }.WithCookies(cookies);

    // ─── POST /api/subjects ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateSubject_ReturnsCreated_WithPreservedCode()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var code = UniqueCode("SCI");
        var response = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "Science",
            code,
            description = "Natural sciences",
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task CreateSubject_NullDescription_ReturnsCreated_WithNullDescription()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "Art",
            code = UniqueCode("ART"),
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task CreateSubject_DuplicateCode_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var code = UniqueCode("DUP");

        var r1 = await client.SendAsync(Post("/api/subjects", cookies, new { name = "Subject A", code }));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await client.SendAsync(Post("/api/subjects", cookies, new { name = "Subject B", code }));
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    [Fact]
    public async Task CreateSubject_MissingName_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            code = "NONAME",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubject_MissingCode_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "No Code Subject",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubject_CodeWithSpaces_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "Bad Code",
            code = "BAD CODE",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── GET /api/subjects ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSubjects_IsActiveTrue_ReturnsOnlyActiveSubjects()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "Inactive Subject",
            code = UniqueCode("INACT"),
        }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/subjects/{id}", cookies, new
        {
            name = "Inactive Subject",
            isActive = false,
        }));

        var response = await client.SendAsync(Get("/api/subjects?isActive=true", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, item => Assert.True(item.GetProperty("isActive").GetBoolean()));
    }

    [Fact]
    public async Task GetSubjects_IsActiveFalse_ReturnsOnlyInactiveSubjects()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "To Deactivate",
            code = UniqueCode("DEACT"),
        }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/subjects/{id}", cookies, new
        {
            name = "To Deactivate",
            isActive = false,
        }));

        var response = await client.SendAsync(Get("/api/subjects?isActive=false", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.False(item.GetProperty("isActive").GetBoolean()));
    }

    [Fact]
    public async Task GetSubjects_Search_ReturnsMatchingSubjects()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var tag = Guid.NewGuid().ToString("N")[..8].ToUpper();
        await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = $"AlgebraSearch{tag}",
            code = $"ALG{tag[..6]}",
        }));

        var response = await client.SendAsync(Get($"/api/subjects?search=AlgebraSearch{tag}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item =>
            Assert.Contains(tag, item.GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetSubjects_PageAndPageSize_ReturnsPaginationFields()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        for (var i = 0; i < 3; i++)
            await client.SendAsync(Post("/api/subjects", cookies, new
            {
                name = $"PaginationSubject{i}",
                code = UniqueCode($"PAG{i}"),
            }));

        var response = await client.SendAsync(Get("/api/subjects?page=1&pageSize=2", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 3);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetSubjects_PageSizeOver100_ClampedTo100()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get("/api/subjects?pageSize=200", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
    }

    // ─── GET /api/subjects/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsFullSubjectDto()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "History",
            code = UniqueCode("HIST"),
            description = "World history",
        }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(Get($"/api/subjects/{id}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal("World history", body.GetProperty("description").GetString());
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get($"/api/subjects/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/subjects/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateSubject_Returns200_WithUpdatedName()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "Old Name",
            code = UniqueCode("UPD"),
        }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(Put($"/api/subjects/{id}", cookies, new
        {
            name = "New Name",
            isActive = true,
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("New Name", body.GetProperty("name").GetString());
        Assert.NotNull(body.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task UpdateSubject_CodeIsImmutable()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var code = UniqueCode("IMMUT");
        var create = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "Immutable Code",
            code,
        }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var update = await client.SendAsync(Put($"/api/subjects/{id}", cookies, new
        {
            name = "Updated Name",
            isActive = true,
        }));

        var body = await update.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateSubject_Deactivate_AbsentFromActiveList()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "Will Deactivate",
            code = UniqueCode("WD"),
        }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/subjects/{id}", cookies, new
        {
            name = "Will Deactivate",
            isActive = false,
        }));

        var listResponse = await client.SendAsync(Get("/api/subjects?isActive=true", cookies));
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task UpdateSubject_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Put($"/api/subjects/{Guid.NewGuid()}", cookies, new
        {
            name = "Ghost",
            isActive = true,
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── No DELETE ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSubject_Returns405_MethodNotAllowed()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/subjects", cookies, new
        {
            name = "Delete Test",
            code = UniqueCode("DEL"),
        }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"/api/subjects/{id}").WithCookies(cookies));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/subjects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeacherRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        const string teacherEmail = "subject-auth-gate-teacher@demoschool.test";
        const string teacherPassword = "TeacherPass1!";
        const string schoolId = "00000000-0000-0000-0000-000000000001";

        using (var scope = factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var alreadyExists = await db.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == teacherEmail);
            if (!alreadyExists)
            {
                db.Users.Add(new User
                {
                    SchoolId = Guid.Parse(schoolId),
                    Email = teacherEmail,
                    PasswordHash = hasher.HashPassword(teacherPassword),
                    DisplayName = "Subject Auth Gate Teacher",
                    Role = UserRole.Teacher,
                    IsActive = true,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginResponse);

        // GET /api/subjects (list) was intentionally relaxed to Admin+Teacher (commit 11d83d4) so
        // teachers can read the subject catalog. Admin-only subject *management* endpoints — e.g.
        // GET /api/subjects/{id} — must still forbid a teacher.
        var response = await client.SendAsync(Get($"/api/subjects/{Guid.NewGuid()}", teacherCookies));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
