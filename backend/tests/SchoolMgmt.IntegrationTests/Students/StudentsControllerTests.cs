using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;
using SchoolMgmt.Infrastructure.Persistence;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Students;

[Collection(IntegrationTestCollection.Name)]
public class StudentsControllerTests(PostgresContainerFixture fixture)
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

    private static object ValidCreatePayload(int year = 2025) => new
    {
        firstName = "Jane",
        lastName = "Doe",
        dateOfBirth = "2010-05-15",
        gender = "Female",
        enrollmentDate = $"{year}-09-01",
        guardianName = "John Doe",
        guardianPhone = "555-1234",
        guardianEmail = "john.doe@example.com",
    };

    // ─── POST /api/students ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateStudent_ReturnsCreated_WithStudentCode()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2025)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Matches(@"^2025-\d{6}$", body.GetProperty("studentCode").GetString()!);
        Assert.Equal("Active", body.GetProperty("enrollmentStatus").GetString());
        Assert.Equal("John Doe", body.GetProperty("guardianName").GetString());
        Assert.Equal("john.doe@example.com", body.GetProperty("guardianEmail").GetString());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task CreateStudent_MissingFirstName_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/students", cookies, new
        {
            lastName = "Doe",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2025-09-01",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_InvalidGender_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/students", cookies, new
        {
            firstName = "Jane",
            lastName = "Doe",
            dateOfBirth = "2010-05-15",
            gender = "Unknown",
            enrollmentDate = "2025-09-01",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_InvalidGuardianEmail_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/students", cookies, new
        {
            firstName = "Jane",
            lastName = "Doe",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2025-09-01",
            guardianEmail = "not-an-email",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_TwoSequentialInSameYear_StudentCodesIncrement()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var r1 = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2030)));
        var r2 = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2030)));

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        var b1 = await r1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var b2 = await r2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var code1 = b1.GetProperty("studentCode").GetString()!;
        var code2 = b2.GetProperty("studentCode").GetString()!;

        Assert.StartsWith("2030-", code1);
        Assert.StartsWith("2030-", code2);
        Assert.NotEqual(code1, code2);

        var seq1 = int.Parse(code1.Split('-')[1]);
        var seq2 = int.Parse(code2.Split('-')[1]);
        Assert.Equal(seq1 + 1, seq2);
    }

    [Fact]
    public async Task CreateStudent_DifferentEnrollmentYear_StudentCodePrefixMatchesYear()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2024)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.StartsWith("2024-", body.GetProperty("studentCode").GetString()!);
    }

    // ─── GET /api/students ───────────────────────────────────────────────────

    [Fact]
    public async Task GetStudents_NoParams_ReturnsOnlyActiveStudents()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        // Create a student, then transfer them
        var create = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2031)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/students/{id}", cookies, new
        {
            firstName = "Jane",
            lastName = "Doe",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2031-09-01",
            enrollmentStatus = "Transferred",
        }));

        var response = await client.SendAsync(Get("/api/students", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, item =>
            Assert.Equal("Active", item.GetProperty("enrollmentStatus").GetString()));
    }

    [Fact]
    public async Task GetStudents_StatusTransferred_ReturnsOnlyTransferredStudents()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2032)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/students/{id}", cookies, new
        {
            firstName = "Jane",
            lastName = "Doe",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2032-09-01",
            enrollmentStatus = "Transferred",
        }));

        var response = await client.SendAsync(Get("/api/students?status=Transferred", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item =>
            Assert.Equal("Transferred", item.GetProperty("enrollmentStatus").GetString()));
    }

    [Fact]
    public async Task GetStudents_PageAndPageSize_ReturnsPaginationFields()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        // Create 3 students for 2033 so we have something to page
        for (var i = 0; i < 3; i++)
            await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2033)));

        var response = await client.SendAsync(Get("/api/students?page=1&pageSize=2", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 3);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetStudents_PageSizeOver100_ClampedTo100()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get("/api/students?pageSize=200", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
    }

    // ─── GET /api/students/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsFullStudentDtoWithGuardianFields()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2034)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(Get($"/api/students/{id}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal("John Doe", body.GetProperty("guardianName").GetString());
        Assert.Equal("555-1234", body.GetProperty("guardianPhone").GetString());
        Assert.Equal("john.doe@example.com", body.GetProperty("guardianEmail").GetString());
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get($"/api/students/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/students/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateStudent_Returns200_WithUpdatedName()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2035)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;
        var originalCode = created.GetProperty("studentCode").GetString()!;

        var response = await client.SendAsync(Put($"/api/students/{id}", cookies, new
        {
            firstName = "Janet",
            lastName = "Smith",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2035-09-01",
            enrollmentStatus = "Active",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Janet", body.GetProperty("firstName").GetString());
        Assert.Equal("Smith", body.GetProperty("lastName").GetString());
        Assert.NotNull(body.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task UpdateStudent_StatusTransferred_NoLongerAppearsInDefaultList()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2036)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/students/{id}", cookies, new
        {
            firstName = "Jane",
            lastName = "Doe",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2036-09-01",
            enrollmentStatus = "Transferred",
        }));

        var listResponse = await client.SendAsync(Get("/api/students", cookies));
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task UpdateStudent_InvalidEnrollmentStatus_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2037)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(Put($"/api/students/{id}", cookies, new
        {
            firstName = "Jane",
            lastName = "Doe",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2037-09-01",
            enrollmentStatus = "InvalidStatus",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStudent_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Put($"/api/students/{Guid.NewGuid()}", cookies, new
        {
            firstName = "Jane",
            lastName = "Doe",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2025-09-01",
            enrollmentStatus = "Active",
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStudent_StudentCodeIsImmutable()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2038)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;
        var originalCode = created.GetProperty("studentCode").GetString()!;

        var update = await client.SendAsync(Put($"/api/students/{id}", cookies, new
        {
            firstName = "Janet",
            lastName = "Smith",
            dateOfBirth = "2010-05-15",
            gender = "Female",
            enrollmentDate = "2038-09-01",
            enrollmentStatus = "Active",
        }));

        var body = await update.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(originalCode, body.GetProperty("studentCode").GetString());
    }

    // ─── No DELETE ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteStudent_Returns405_MethodNotAllowed()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/students", cookies, ValidCreatePayload(2039)));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString()!;

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"/api/students/{id}").WithCookies(cookies));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/students");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeacherRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        const string teacherEmail = "teacher-students@demoschool.test";
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
                    DisplayName = "Students Teacher",
                    Role = UserRole.Teacher,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginResponse);

        var response = await client.SendAsync(Get("/api/students", teacherCookies));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
