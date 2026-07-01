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

namespace SchoolMgmt.IntegrationTests.Grades;

[Collection(IntegrationTestCollection.Name)]
public class GradesControllerTests(PostgresContainerFixture fixture)
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

    private static HttpRequestMessage Post(string url, string cookies, object? body = null)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, url).WithCookies(cookies);
        if (body is not null) msg.Content = JsonContent.Create(body);
        return msg;
    }

    private static HttpRequestMessage Put(string url, string cookies, object body) =>
        new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) }.WithCookies(cookies);

    private static HttpRequestMessage Delete(string url, string cookies) =>
        new HttpRequestMessage(HttpMethod.Delete, url).WithCookies(cookies);

    // ─── POST /api/grades ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGrade_ReturnsCreated_WithEmptySections()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade 1", displayOrder = 1 }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Grade 1", body.GetProperty("name").GetString());
        Assert.Equal(1, body.GetProperty("displayOrder").GetInt32());
        Assert.Equal(0, body.GetProperty("sections").GetArrayLength());
    }

    [Fact]
    public async Task CreateGrade_DuplicateName_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var payload = new { name = "Grade 2-dup", displayOrder = 2 };
        await client.SendAsync(Post("/api/grades", cookies, payload));
        var response = await client.SendAsync(Post("/api/grades", cookies, payload));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ─── GET /api/grades ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsGradesOrderedByDisplayOrder()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Z-order-10", displayOrder = 10 }));
        await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Z-order-5", displayOrder = 5 }));

        var response = await client.SendAsync(Get("/api/grades", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(body.GetArrayLength() >= 2);

        var orders = body.EnumerateArray()
            .Select(g => g.GetProperty("displayOrder").GetInt32())
            .ToList();
        Assert.Equal(orders.OrderBy(x => x).ToList(), orders);
    }

    // ─── GET /api/grades/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get($"/api/grades/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/grades/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateGrade_Returns200_WithUpdatedData()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Update-orig", displayOrder = 99 }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/grades/{id}", cookies,
            new { name = "Grade Update-new", displayOrder = 88 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Grade Update-new", updated.GetProperty("name").GetString());
        Assert.Equal(88, updated.GetProperty("displayOrder").GetInt32());

        // Re-fetch confirms persistence
        var refetch = await client.SendAsync(Get($"/api/grades/{id}", cookies));
        var refetched = await refetch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Grade Update-new", refetched.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdateGrade_DuplicateName_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Conflict-A", displayOrder = 1 }));
        var createB = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Conflict-B", displayOrder = 2 }));
        var gradeB = await createB.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var idB = gradeB.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/grades/{idB}", cookies,
            new { name = "Grade Conflict-A", displayOrder = 2 }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ─── DELETE /api/grades/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteGrade_EmptyGrade_Returns204_AndGradeIsGone()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Delete-empty", displayOrder = 50 }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString();

        var del = await client.SendAsync(Delete($"/api/grades/{id}", cookies));
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var refetch = await client.SendAsync(Get($"/api/grades/{id}", cookies));
        Assert.Equal(HttpStatusCode.NotFound, refetch.StatusCode);
    }

    [Fact]
    public async Task DeleteGrade_WithSections_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var create = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Delete-with-sections", displayOrder = 60 }));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = created.GetProperty("id").GetString();

        await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies, new { name = "A" }));

        var del = await client.SendAsync(Delete($"/api/grades/{gradeId}", cookies));
        Assert.Equal(HttpStatusCode.BadRequest, del.StatusCode);
    }

    // ─── POST /api/grades/{gradeId}/sections ─────────────────────────────────

    [Fact]
    public async Task AddSection_Returns201_AndGradeIncludesSection()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var createGrade = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Section-add", displayOrder = 70 }));
        var grade = await createGrade.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString();

        var response = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies, new { name = "A" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var refetch = await client.SendAsync(Get($"/api/grades/{gradeId}", cookies));
        var refetched = await refetch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, refetched.GetProperty("sections").GetArrayLength());
        Assert.Equal("A", refetched.GetProperty("sections")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task AddSection_DuplicateNameInSameGrade_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var createGrade = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Section-dup", displayOrder = 71 }));
        var grade = await createGrade.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString();

        await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies, new { name = "A" }));
        var response = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies, new { name = "A" }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddSection_SameNameInDifferentGrade_Returns201()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var g1 = await (await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Section-cross-1", displayOrder = 72 }))).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var g2 = await (await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Section-cross-2", displayOrder = 73 }))).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        await client.SendAsync(Post($"/api/grades/{g1.GetProperty("id").GetString()}/sections", cookies, new { name = "A" }));
        var response = await client.SendAsync(Post($"/api/grades/{g2.GetProperty("id").GetString()}/sections", cookies, new { name = "A" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ─── PUT /api/grades/{gradeId}/sections/{sectionId} ──────────────────────

    [Fact]
    public async Task UpdateSection_Returns200_AndGradeReflectsChange()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var createGrade = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Section-update", displayOrder = 80 }));
        var grade = await createGrade.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString();

        var createSection = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies, new { name = "A" }));
        var section = await createSection.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var sectionId = section.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/grades/{gradeId}/sections/{sectionId}", cookies, new { name = "A-renamed" }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refetch = await client.SendAsync(Get($"/api/grades/{gradeId}", cookies));
        var refetched = await refetch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("A-renamed", refetched.GetProperty("sections")[0].GetProperty("name").GetString());
    }

    // ─── DELETE /api/grades/{gradeId}/sections/{sectionId} ───────────────────

    [Fact]
    public async Task DeleteSection_Returns204_AndSectionIsGone()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var createGrade = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Section-delete", displayOrder = 90 }));
        var grade = await createGrade.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString();

        var createSection = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies, new { name = "A" }));
        var section = await createSection.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var sectionId = section.GetProperty("id").GetString();

        var del = await client.SendAsync(Delete($"/api/grades/{gradeId}/sections/{sectionId}", cookies));
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var refetch = await client.SendAsync(Get($"/api/grades/{gradeId}", cookies));
        var refetched = await refetch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, refetched.GetProperty("sections").GetArrayLength());
    }

    [Fact]
    public async Task DeleteSection_UnknownSectionId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var createGrade = await client.SendAsync(Post("/api/grades", cookies, new { name = "Grade Section-del-404", displayOrder = 91 }));
        var grade = await createGrade.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString();

        var response = await client.SendAsync(Delete($"/api/grades/{gradeId}/sections/{Guid.NewGuid()}", cookies));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/grades");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeacherRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        const string teacherEmail = "teacher-grades@demoschool.test";
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
                    DisplayName = "Grades Teacher",
                    Role = UserRole.Teacher,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginResponse);

        var response = await client.SendAsync(Get("/api/grades", teacherCookies));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
