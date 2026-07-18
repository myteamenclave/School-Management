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

namespace SchoolMgmt.IntegrationTests.AcademicYears;

[Collection(IntegrationTestCollection.Name)]
public class AcademicYearsControllerTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Logs in as demo Admin and returns cookie header.
    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@demoschool.test", password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return CookieTestHelpers.BuildCookieHeader(loginResponse);
    }

    private static HttpRequestMessage Get(string url, string cookies) =>
        new HttpRequestMessage(HttpMethod.Get, url).WithCookies(cookies);

    private static HttpRequestMessage Post(string url, string cookies, object? body = null)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, url).WithCookies(cookies);
        if (body is not null) msg.Content = JsonContent.Create(body);
        return msg;
    }

    private static HttpRequestMessage Put(string url, string cookies, object body) =>
        new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) }.WithCookies(cookies);

    // ─── POST /api/academic-years ─────────────────────────────────────────────

    [Fact]
    public async Task CreateYear_ReturnsCreated_WithTwoSemesters()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2024-2025",
            startDate = "2024-09-01",
            endDate = "2025-06-30"
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("2024-2025", body.GetProperty("name").GetString());
        Assert.Equal(2, body.GetProperty("semesters").GetArrayLength());
    }

    [Fact]
    public async Task CreateYear_DuplicateName_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var payload = new { name = "2025-duplicate", startDate = "2025-09-01", endDate = "2026-06-30" };
        await client.SendAsync(Post("/api/academic-years", cookies, payload));
        var response = await client.SendAsync(Post("/api/academic-years", cookies, payload));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ─── GET /api/academic-years ──────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsYearsWithSemesters()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2026-getall",
            startDate = "2026-09-01",
            endDate = "2027-06-30"
        }));

        var response = await client.SendAsync(Get("/api/academic-years", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(body.GetArrayLength() > 0);
        var first = body[0];
        Assert.True(first.GetProperty("semesters").GetArrayLength() >= 1);
    }

    // ─── GET /api/academic-years/{id} ────────────────────────────────────────

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get($"/api/academic-years/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/academic-years/{yearId}/semesters/{semesterId} ─────────────

    [Fact]
    public async Task UpdateSemester_Returns200_WithUpdatedData()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2027-update",
            startDate = "2027-09-01",
            endDate = "2028-06-30"
        }));
        var year = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearId = year.GetProperty("id").GetString();
        var semesterId = year.GetProperty("semesters")[0].GetProperty("id").GetString();

        var response = await client.SendAsync(Put(
            $"/api/academic-years/{yearId}/semesters/{semesterId}", cookies,
            new { name = "Term 1 Updated", startDate = "2027-09-01", endDate = "2028-01-31" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Term 1 Updated", updated.GetProperty("name").GetString());
    }

    // ─── POST /api/academic-years/{id}/set-current ───────────────────────────

    [Fact]
    public async Task SetCurrent_SetsYearAndSemester1AsCurrent()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2028-setcurrent",
            startDate = "2028-09-01",
            endDate = "2029-06-30"
        }));
        var year = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearId = year.GetProperty("id").GetString();

        var setCurrent = await client.SendAsync(Post($"/api/academic-years/{yearId}/set-current", cookies));
        Assert.Equal(HttpStatusCode.NoContent, setCurrent.StatusCode);

        var refetch = await client.SendAsync(Get($"/api/academic-years/{yearId}", cookies));
        var refetched = await refetch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(refetched.GetProperty("isCurrent").GetBoolean());

        var sem1 = refetched.GetProperty("semesters").EnumerateArray()
            .Single(s => s.GetProperty("name").GetString() == "Semester 1");
        Assert.True(sem1.GetProperty("isCurrent").GetBoolean());
    }

    [Fact]
    public async Task SetCurrent_UnsetsPreviousCurrentYear()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        // Create and set year A as current
        var createA = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2029-prev",
            startDate = "2029-09-01",
            endDate = "2030-06-30"
        }));
        var yearA = await createA.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearAId = yearA.GetProperty("id").GetString();
        await client.SendAsync(Post($"/api/academic-years/{yearAId}/set-current", cookies));

        // Create year B and set it as current
        var createB = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2030-new",
            startDate = "2030-09-01",
            endDate = "2031-06-30"
        }));
        var yearB = await createB.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearBId = yearB.GetProperty("id").GetString();
        await client.SendAsync(Post($"/api/academic-years/{yearBId}/set-current", cookies));

        // Year A should no longer be current
        var refetchA = await client.SendAsync(Get($"/api/academic-years/{yearAId}", cookies));
        var refetchedA = await refetchA.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(refetchedA.GetProperty("isCurrent").GetBoolean());
    }

    // ─── POST /api/academic-years/{yearId}/semesters/{semesterId}/set-current ─

    [Fact]
    public async Task SetCurrentSemester_OverridesToSemester2()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2031-sem",
            startDate = "2031-09-01",
            endDate = "2032-06-30"
        }));
        var year = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearId = year.GetProperty("id").GetString();
        await client.SendAsync(Post($"/api/academic-years/{yearId}/set-current", cookies));

        // Now switch to Semester 2
        var refetch = await client.SendAsync(Get($"/api/academic-years/{yearId}", cookies));
        var refetched = await refetch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var sem2Id = refetched.GetProperty("semesters").EnumerateArray()
            .Single(s => s.GetProperty("name").GetString() == "Semester 2")
            .GetProperty("id").GetString();

        var setSem = await client.SendAsync(Post(
            $"/api/academic-years/{yearId}/semesters/{sem2Id}/set-current", cookies));
        Assert.Equal(HttpStatusCode.NoContent, setSem.StatusCode);

        // Semester 1 should no longer be current
        var afterSwitch = await client.SendAsync(Get($"/api/academic-years/{yearId}", cookies));
        var afterSwitchBody = await afterSwitch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var sem1 = afterSwitchBody.GetProperty("semesters").EnumerateArray()
            .Single(s => s.GetProperty("name").GetString() == "Semester 1");
        Assert.False(sem1.GetProperty("isCurrent").GetBoolean());
    }

    [Fact]
    public async Task SetCurrentSemester_FromNonCurrentYear_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        // Create year A (current) and year B (not current)
        var createA = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2032-cur",
            startDate = "2032-09-01",
            endDate = "2033-06-30"
        }));
        var yearA = await createA.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearAId = yearA.GetProperty("id").GetString();
        await client.SendAsync(Post($"/api/academic-years/{yearAId}/set-current", cookies));

        var createB = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2033-noncur",
            startDate = "2033-09-01",
            endDate = "2034-06-30"
        }));
        var yearB = await createB.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearBId = yearB.GetProperty("id").GetString();
        var semBId = yearB.GetProperty("semesters")[0].GetProperty("id").GetString();

        var response = await client.SendAsync(Post(
            $"/api/academic-years/{yearBId}/semesters/{semBId}/set-current", cookies));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── POST /api/academic-years/{id}/archive ────────────────────────────────

    [Fact]
    public async Task Archive_NonCurrentYear_Succeeds_AndBlocksSemesterUpdate()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2034-archive",
            startDate = "2034-09-01",
            endDate = "2035-06-30"
        }));
        var year = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearId = year.GetProperty("id").GetString();
        var semId = year.GetProperty("semesters")[0].GetProperty("id").GetString();

        var archive = await client.SendAsync(Post($"/api/academic-years/{yearId}/archive", cookies));
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        // Updating a semester on an archived year should return 400
        var update = await client.SendAsync(Put(
            $"/api/academic-years/{yearId}/semesters/{semId}", cookies,
            new { name = "Should fail", startDate = "2034-09-01", endDate = "2035-01-31" }));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Archive_CurrentYear_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = "2035-archcur",
            startDate = "2035-09-01",
            endDate = "2036-06-30"
        }));
        var year = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearId = year.GetProperty("id").GetString();
        await client.SendAsync(Post($"/api/academic-years/{yearId}/set-current", cookies));

        var response = await client.SendAsync(Post($"/api/academic-years/{yearId}/archive", cookies));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/academic-years");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Academic years are readable by any authenticated user — see commit
    // "allow any authenticated user to read academic years and grades". Teachers
    // need the year/semester list to drive attendance and gradebook pickers.
    [Fact]
    public async Task TeacherRole_CanReadList_Returns200()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        // Seed a Teacher user directly — DemoDataSeeder only creates an Admin.
        const string teacherEmail = "teacher@demoschool.test";
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
                    DisplayName = "Demo Teacher",
                    Role = UserRole.Teacher,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginResponse);

        var response = await client.SendAsync(Get("/api/academic-years", teacherCookies));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
