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

namespace SchoolMgmt.IntegrationTests.TeacherAssignments;

[Collection(IntegrationTestCollection.Name)]
public class TeacherAssignmentsControllerTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    private static async Task<string> SeedGradeWithSectionAsync(HttpClient client, string cookies, string gradeName)
    {
        var gradeRes = await client.SendAsync(Post("/api/grades", cookies,
            new { name = gradeName, displayOrder = 70 }));
        Assert.Equal(HttpStatusCode.Created, gradeRes.StatusCode);
        var grade = await gradeRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString()!;

        var secRes = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies,
            new { name = "5-A" }));
        var section = await secRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return section.GetProperty("id").GetString()!;
    }

    private static async Task<(string sectionAId, string sectionBId)> SeedGradeWithTwoSectionsAsync(
        HttpClient client, string cookies, string gradeName)
    {
        var gradeRes = await client.SendAsync(Post("/api/grades", cookies,
            new { name = gradeName, displayOrder = 70 }));
        var grade = await gradeRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString()!;

        var secA = await (await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies,
            new { name = "5-A" }))).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var secB = await (await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies,
            new { name = "5-B" }))).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return (secA.GetProperty("id").GetString()!, secB.GetProperty("id").GetString()!);
    }

    private static async Task<string> SeedAcademicYearAsync(HttpClient client, string cookies, string name)
    {
        var res = await client.SendAsync(Post("/api/academic-years", cookies,
            new { name, startDate = "2025-09-01", endDate = "2026-06-30" }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    private static async Task<string> SeedSubjectAsync(HttpClient client, string cookies, string name, string code)
    {
        var res = await client.SendAsync(Post("/api/subjects", cookies,
            new { name, code }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    private static async Task<string> SeedTeacherAsync(HttpClient client, string cookies, string suffix)
    {
        var res = await client.SendAsync(Post("/api/teachers", cookies, new
        {
            email = $"teacher-{suffix}@demoschool.test",
            password = "Passw0rd!",
            firstName = "Test",
            lastName = "Teacher",
            phone = "555-0200",
            joiningDate = "2025-09-01",
        }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    // ─── POST /api/teachers/{teacherId}/assignments ───────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201_WithDetails()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TA1-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TA1-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Math-" + tag, "MATH-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "ta1-" + tag);

        var response = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(teacherId, body.GetProperty("teacherId").GetString());
        Assert.Equal(subjectId, body.GetProperty("subjectId").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("subjectName").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("sectionName").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("gradeName").GetString()));
    }

    [Fact]
    public async Task Create_UnknownSubject_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TA2-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TA2-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "ta2-" + tag);

        var response = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId = Guid.NewGuid(), sectionId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownSection_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TA3-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Physics-" + tag, "PHYS-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "ta3-" + tag);

        var response = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId = Guid.NewGuid(), academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownAcademicYear_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TA4-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Chem-" + tag, "CHEM-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "ta4-" + tag);

        var response = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownTeacher_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TA5-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TA5-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Bio-" + tag, "BIO-" + tag);

        var response = await client.SendAsync(Post($"/api/teachers/{Guid.NewGuid()}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameTeacherSameSlotTwice_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TA6-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TA6-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Eng-" + tag, "ENG-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "ta6-" + tag);

        await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));
        var response = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_DifferentTeacher_SameSlot_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TA7-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TA7-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Art-" + tag, "ART-" + tag);
        var teacher1Id = await SeedTeacherAsync(client, cookies, "ta7a-" + tag);
        var teacher2Id = await SeedTeacherAsync(client, cookies, "ta7b-" + tag);

        await client.SendAsync(Post($"/api/teachers/{teacher1Id}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));
        var response = await client.SendAsync(Post($"/api/teachers/{teacher2Id}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameTeacher_SameSubject_DifferentSection_Returns201()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionAId, sectionBId) = await SeedGradeWithTwoSectionsAsync(client, cookies, "Grade-TA8-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TA8-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Hist-" + tag, "HIST-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "ta8-" + tag);

        await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId = sectionAId, academicYearId = yearId }));
        var response = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId = sectionBId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingSubjectId_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TA9-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TA9-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "ta9-" + tag);

        var response = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId = Guid.Empty, sectionId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── GET /api/teachers/{teacherId}/assignments ────────────────────────────

    [Fact]
    public async Task GetByTeacherAndYear_AfterTwoAssignments_Returns200_Ordered()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionAId, sectionBId) = await SeedGradeWithTwoSectionsAsync(client, cookies, "Grade-TG1-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TG1-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Music-" + tag, "MUS-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "tg1-" + tag);

        await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId = sectionAId, academicYearId = yearId }));
        await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId = sectionBId, academicYearId = yearId }));

        var response = await client.SendAsync(
            Get($"/api/teachers/{teacherId}/assignments?academicYearId={yearId}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(2, body.GetArrayLength());
    }

    [Fact]
    public async Task GetByTeacherAndYear_MissingAcademicYearId_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var teacherId = await SeedTeacherAsync(client, cookies, "tg2-" + tag);

        var response = await client.SendAsync(Get($"/api/teachers/{teacherId}/assignments", cookies));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetByTeacherAndYear_UnknownTeacher_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TG3-" + tag);

        var response = await client.SendAsync(
            Get($"/api/teachers/{Guid.NewGuid()}/assignments?academicYearId={yearId}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── DELETE /api/teachers/{teacherId}/assignments/{id} ────────────────────

    [Fact]
    public async Task Delete_Returns204_AndRemovedFromList()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TD1-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TD1-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "PE-" + tag, "PE-" + tag);
        var teacherId = await SeedTeacherAsync(client, cookies, "td1-" + tag);

        var createRes = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));
        var assignmentId = (await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        var deleteRes = await client.SendAsync(Delete(
            $"/api/teachers/{teacherId}/assignments/{assignmentId}", cookies));
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var list = await (await client.SendAsync(
            Get($"/api/teachers/{teacherId}/assignments?academicYearId={yearId}", cookies)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var teacherId = await SeedTeacherAsync(client, cookies, "td2-" + tag);

        var response = await client.SendAsync(Delete(
            $"/api/teachers/{teacherId}/assignments/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_CrossTeacher_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TD3-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TD3-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "IT-" + tag, "IT-" + tag);
        var teacher1Id = await SeedTeacherAsync(client, cookies, "td3a-" + tag);
        var teacher2Id = await SeedTeacherAsync(client, cookies, "td3b-" + tag);

        var createRes = await client.SendAsync(Post($"/api/teachers/{teacher1Id}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));
        var teacher1AssignmentId = (await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        var response = await client.SendAsync(Delete(
            $"/api/teachers/{teacher2Id}/assignments/{teacher1AssignmentId}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenReassign_Returns201()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedGradeWithSectionAsync(client, cookies, "Grade-TD4-" + tag);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-TD4-" + tag);
        var subjectId = await SeedSubjectAsync(client, cookies, "Geo-" + tag, "GEO-" + tag);
        var teacher1Id = await SeedTeacherAsync(client, cookies, "td4a-" + tag);
        var teacher2Id = await SeedTeacherAsync(client, cookies, "td4b-" + tag);

        var createRes = await client.SendAsync(Post($"/api/teachers/{teacher1Id}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));
        var assignmentId = (await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        await client.SendAsync(Delete($"/api/teachers/{teacher1Id}/assignments/{assignmentId}", cookies));

        var reassign = await client.SendAsync(Post($"/api/teachers/{teacher2Id}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));
        Assert.Equal(HttpStatusCode.Created, reassign.StatusCode);
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/teachers/{Guid.NewGuid()}/assignments?academicYearId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeacherRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        const string teacherEmail = "teacher-ta-auth@demoschool.test";
        const string teacherPassword = "TeacherPass1!";

        using (var scope = factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == teacherEmail))
            {
                db.Users.Add(new User
                {
                    SchoolId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Email = teacherEmail,
                    PasswordHash = hasher.HashPassword(teacherPassword),
                    DisplayName = "Teacher TA Auth",
                    Role = UserRole.Teacher,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginRes);

        var response = await client.SendAsync(
            Get($"/api/teachers/{Guid.NewGuid()}/assignments?academicYearId={Guid.NewGuid()}", teacherCookies));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
