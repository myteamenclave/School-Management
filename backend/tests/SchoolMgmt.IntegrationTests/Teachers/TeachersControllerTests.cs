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

namespace SchoolMgmt.IntegrationTests.Teachers;

[Collection(IntegrationTestCollection.Name)]
public class TeachersControllerTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    private static object ValidCreatePayload(int year = 2026, string emailSuffix = "") => new
    {
        email = $"teacher{emailSuffix}-{Guid.NewGuid():N}@demoschool.test",
        password = "Passw0rd!",
        firstName = "Alice",
        lastName = "Smith",
        phone = "555-0100",
        joiningDate = $"{year}-09-01",
    };

    // ─── POST /api/teachers ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateTeacher_ReturnsCreated_WithTeacherCode()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2026)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Matches(@"^2026-\d{6}$", body.GetProperty("teacherCode").GetString()!);
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.Contains("@demoschool.test", body.GetProperty("email").GetString()!);
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task CreateTeacher_TwoSequentialInSameYear_CodesIncrement()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var r1 = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2040)));
        var r2 = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2040)));

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        var b1 = await r1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var b2 = await r2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var code1 = b1.GetProperty("teacherCode").GetString()!;
        var code2 = b2.GetProperty("teacherCode").GetString()!;

        Assert.StartsWith("2040-", code1);
        Assert.StartsWith("2040-", code2);

        var seq1 = int.Parse(code1.Split('-')[1]);
        var seq2 = int.Parse(code2.Split('-')[1]);
        Assert.Equal(seq1 + 1, seq2);
    }

    [Fact]
    public async Task CreateTeacher_DifferentJoiningYear_CodePrefixMatchesYear()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2025)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.StartsWith("2025-", body.GetProperty("teacherCode").GetString()!);
    }

    [Fact]
    public async Task CreateTeacher_DuplicateEmail_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var payload = new
        {
            email = $"dup-teacher-{Guid.NewGuid():N}@demoschool.test",
            password = "Passw0rd!",
            firstName = "Dup",
            lastName = "Teacher",
            joiningDate = "2026-09-01",
        };

        var r1 = await client.SendAsync(Post("/api/teachers", cookies, payload));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await client.SendAsync(Post("/api/teachers", cookies, payload));
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    [Fact]
    public async Task CreateTeacher_MissingFirstName_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/teachers", cookies, new
        {
            email = "missing-fname@demoschool.test",
            password = "Passw0rd!",
            lastName = "Smith",
            joiningDate = "2026-09-01",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTeacher_InvalidEmail_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/teachers", cookies, new
        {
            email = "not-an-email",
            password = "Passw0rd!",
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2026-09-01",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTeacher_ShortPassword_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/teachers", cookies, new
        {
            email = $"short-pwd-{Guid.NewGuid():N}@demoschool.test",
            password = "short",
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2026-09-01",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── GET /api/teachers ───────────────────────────────────────────────────

    [Fact]
    public async Task GetTeachers_IsActiveTrue_ReturnsOnlyActiveTeachers()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        // Create a teacher and then deactivate them
        var create = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2041)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/teachers/{id}", cookies, new
        {
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2041-09-01",
            isActive = false,
        }));

        var response = await client.SendAsync(Get("/api/teachers?isActive=true", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, item => Assert.True(item.GetProperty("isActive").GetBoolean()));
    }

    [Fact]
    public async Task GetTeachers_IsActiveFalse_ReturnsOnlyInactiveTeachers()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2042)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/teachers/{id}", cookies, new
        {
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2042-09-01",
            isActive = false,
        }));

        var response = await client.SendAsync(Get("/api/teachers?isActive=false", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.False(item.GetProperty("isActive").GetBoolean()));
    }

    [Fact]
    public async Task GetTeachers_PageAndPageSize_ReturnsPaginationFields()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        for (var i = 0; i < 3; i++)
            await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2043)));

        var response = await client.SendAsync(Get("/api/teachers?page=1&pageSize=2", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 3);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetTeachers_PageSizeOver100_ClampedTo100()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get("/api/teachers?pageSize=200", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
    }

    // ─── GET /api/teachers/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsFullTeacherDtoWithEmailAndUserId()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2044)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(Get($"/api/teachers/{id}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Contains("@demoschool.test", body.GetProperty("email").GetString()!);
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("userId").GetString()!));
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get($"/api/teachers/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/teachers/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateTeacher_Returns200_WithUpdatedName()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2045)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(Put($"/api/teachers/{id}", cookies, new
        {
            firstName = "Updated",
            lastName = "Name",
            joiningDate = "2045-09-01",
            isActive = true,
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Updated", body.GetProperty("firstName").GetString());
        Assert.Equal("Name", body.GetProperty("lastName").GetString());
        Assert.NotNull(body.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task UpdateTeacher_DeactivateTeacher_AbsentFromActiveList()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2046)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/teachers/{id}", cookies, new
        {
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2046-09-01",
            isActive = false,
        }));

        var listResponse = await client.SendAsync(Get("/api/teachers?isActive=true", cookies));
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task UpdateTeacher_DeactivateTeacher_LoginReturns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"deactivated-{Guid.NewGuid():N}@demoschool.test";
        var payload = new
        {
            email,
            password = "Passw0rd!",
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2026-09-01",
        };

        var create = await client.SendAsync(Post("/api/teachers", cookies, payload));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/teachers/{id}", cookies, new
        {
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2026-09-01",
            isActive = false,
        }));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateTeacher_TeacherCodeIsImmutable()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2047)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;
        var originalCode = created.GetProperty("teacherCode").GetString()!;

        var update = await client.SendAsync(Put($"/api/teachers/{id}", cookies, new
        {
            firstName = "New",
            lastName = "Name",
            joiningDate = "2047-09-01",
            isActive = true,
        }));

        var body = await update.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(originalCode, body.GetProperty("teacherCode").GetString());
    }

    [Fact]
    public async Task UpdateTeacher_EmailIsImmutable()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var email = $"immutable-email-{Guid.NewGuid():N}@demoschool.test";
        var create = await client.SendAsync(Post("/api/teachers", cookies, new
        {
            email,
            password = "Passw0rd!",
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2026-09-01",
        }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var update = await client.SendAsync(Put($"/api/teachers/{id}", cookies, new
        {
            firstName = "New",
            lastName = "Name",
            joiningDate = "2026-09-01",
            isActive = true,
        }));

        var body = await update.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(email, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task UpdateTeacher_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Put($"/api/teachers/{Guid.NewGuid()}", cookies, new
        {
            firstName = "Alice",
            lastName = "Smith",
            joiningDate = "2026-09-01",
            isActive = true,
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── No DELETE ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTeacher_Returns405_MethodNotAllowed()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/teachers", cookies, ValidCreatePayload(2048)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"/api/teachers/{id}").WithCookies(cookies));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/teachers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeacherRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        const string teacherEmail = "teacher-auth-gate@demoschool.test";
        const string teacherPassword = "TeacherPass1!";
        const string schoolId = "00000000-0000-0000-0000-000000000001";

        using (var scope = factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var alreadyExists = await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Email == teacherEmail);
            if (!alreadyExists)
            {
                db.Users.Add(new User
                {
                    SchoolId = Guid.Parse(schoolId),
                    Email = teacherEmail,
                    PasswordHash = hasher.HashPassword(teacherPassword),
                    DisplayName = "Auth Gate Teacher",
                    Role = UserRole.Teacher,
                    IsActive = true,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginResponse);

        var response = await client.SendAsync(Get("/api/teachers", teacherCookies));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Auth regression (User.IsActive migration) ───────────────────────────

    [Fact]
    public async Task DemoAdmin_CanStillLoginAfterIsActiveMigration()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@demoschool.test", password = "Passw0rd!" });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }
}
