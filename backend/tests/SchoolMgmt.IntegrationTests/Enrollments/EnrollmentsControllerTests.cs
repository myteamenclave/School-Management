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

namespace SchoolMgmt.IntegrationTests.Enrollments;

[Collection(IntegrationTestCollection.Name)]
public class EnrollmentsControllerTests(PostgresContainerFixture fixture)
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

    private static HttpRequestMessage Put(string url, string cookies, object body) =>
        new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) }.WithCookies(cookies);

    private static HttpRequestMessage Delete(string url, string cookies) =>
        new HttpRequestMessage(HttpMethod.Delete, url).WithCookies(cookies);

    // Helper: seed grade + two sections via API, return (gradeId, sectionAId, sectionBId)
    private static async Task<(string gradeId, string sectionAId, string sectionBId)> SeedGradeWithTwoSectionsAsync(
        HttpClient client, string cookies, string gradeName = "Grade 5-Enroll")
    {
        var gradeRes = await client.SendAsync(Post("/api/grades", cookies,
            new { name = gradeName, displayOrder = 50 }));
        Assert.Equal(HttpStatusCode.Created, gradeRes.StatusCode);
        var grade = await gradeRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString()!;

        var secARes = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies,
            new { name = "5-A" }));
        var secBRes = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies,
            new { name = "5-B" }));
        var sectionA = await secARes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var sectionB = await secBRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return (gradeId, sectionA.GetProperty("id").GetString()!, sectionB.GetProperty("id").GetString()!);
    }

    // Helper: seed academic year via API, return yearId
    private static async Task<string> SeedAcademicYearAsync(HttpClient client, string cookies, string name)
    {
        var res = await client.SendAsync(Post("/api/academic-years", cookies,
            new { name, startDate = "2025-09-01", endDate = "2026-06-30" }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    // Helper: seed student via API, return studentId
    private static async Task<string> SeedStudentAsync(HttpClient client, string cookies,
        string firstName, string lastName)
    {
        var res = await client.SendAsync(Post("/api/students", cookies, new
        {
            firstName,
            lastName,
            dateOfBirth = "2010-03-15",
            gender = "Male",
            enrollmentDate = "2025-09-01",
        }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    // ─── POST /api/sections/{sectionId}/enrollments ───────────────────────────

    [Fact]
    public async Task Enroll_ValidRequest_Returns201_WithStudentAndSectionAndGradeName()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-E1-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-E1-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Alice", "Smith");

        var response = await client.SendAsync(Post(
            $"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(studentId, body.GetProperty("studentId").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("studentCode").GetString()));
        Assert.Equal("Alice", body.GetProperty("studentFirstName").GetString());
        Assert.Equal("Smith", body.GetProperty("studentLastName").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("sectionName").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("gradeName").GetString()));
    }

    [Fact]
    public async Task Enroll_UnknownStudent_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-E2-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-E2-" + Guid.NewGuid().ToString("N")[..6]);

        var response = await client.SendAsync(Post(
            $"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId = Guid.NewGuid(), academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_UnknownAcademicYear_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-E3-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Bob", "Jones");

        var response = await client.SendAsync(Post(
            $"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_UnknownSection_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-E4-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Carol", "Lee");

        var response = await client.SendAsync(Post(
            $"/api/sections/{Guid.NewGuid()}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_SameStudentTwiceInSameYear_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-E5-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-E5-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Dave", "Brown");

        await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        var response = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_MissingStudentId_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-E6-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-E6-" + Guid.NewGuid().ToString("N")[..6]);

        var response = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId = Guid.Empty, academicYearId = yearId }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── GET /api/sections/{sectionId}/enrollments ────────────────────────────

    [Fact]
    public async Task GetEnrollments_AfterEnrollingTwo_ReturnsBothInLastNameOrder()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-G1-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-G1-" + Guid.NewGuid().ToString("N")[..6]);
        var studentAId = await SeedStudentAsync(client, cookies, "Alice", "Zara");
        var studentBId = await SeedStudentAsync(client, cookies, "Bob", "Adams");

        await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId = studentAId, academicYearId = yearId }));
        await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId = studentBId, academicYearId = yearId }));

        var response = await client.SendAsync(
            Get($"/api/sections/{sectionAId}/enrollments?academicYearId={yearId}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(2, body.GetArrayLength());
        Assert.Equal("Adams", body[0].GetProperty("studentLastName").GetString());
        Assert.Equal("Zara", body[1].GetProperty("studentLastName").GetString());
    }

    [Fact]
    public async Task GetEnrollments_MissingAcademicYearId_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-G2-" + Guid.NewGuid().ToString("N")[..6]);

        var response = await client.SendAsync(Get($"/api/sections/{sectionAId}/enrollments", cookies));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEnrollments_NoEnrollments_ReturnsEmptyList()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-G3-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-G3-" + Guid.NewGuid().ToString("N")[..6]);

        var response = await client.SendAsync(
            Get($"/api/sections/{sectionAId}/enrollments?academicYearId={yearId}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task GetEnrollments_UnknownSection_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-G4-" + Guid.NewGuid().ToString("N")[..6]);

        var response = await client.SendAsync(
            Get($"/api/sections/{Guid.NewGuid()}/enrollments?academicYearId={yearId}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/enrollments/{id} (transfer) ─────────────────────────────────

    [Fact]
    public async Task Transfer_ToSectionInSameGrade_Returns200_WithNewSectionName()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, sectionBId) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-T1-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-T1-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Eve", "White");

        var enrollRes = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        var enrollment = await enrollRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var enrollmentId = enrollment.GetProperty("id").GetString()!;
        var originalSectionName = enrollment.GetProperty("sectionName").GetString();

        var response = await client.SendAsync(Put($"/api/enrollments/{enrollmentId}", cookies,
            new { sectionId = sectionBId }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.NotEqual(originalSectionName, body.GetProperty("sectionName").GetString());
        Assert.Equal(sectionBId, body.GetProperty("sectionId").GetString());
    }

    [Fact]
    public async Task Transfer_UnknownSection_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-T2-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-T2-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Frank", "Green");

        var enrollRes = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        var enrollment = await enrollRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var enrollmentId = enrollment.GetProperty("id").GetString()!;

        var response = await client.SendAsync(Put($"/api/enrollments/{enrollmentId}", cookies,
            new { sectionId = Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_UnknownEnrollment_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-T3-" + Guid.NewGuid().ToString("N")[..6]);

        var response = await client.SendAsync(Put($"/api/enrollments/{Guid.NewGuid()}", cookies,
            new { sectionId = sectionAId }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_StudentAppearsInNewSection_NotInOld()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, sectionBId) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-T4-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-T4-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Grace", "Black");

        var enrollRes = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        var enrollmentId = (await enrollRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        await client.SendAsync(Put($"/api/enrollments/{enrollmentId}", cookies,
            new { sectionId = sectionBId }));

        var oldList = await (await client.SendAsync(
            Get($"/api/sections/{sectionAId}/enrollments?academicYearId={yearId}", cookies)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var newList = await (await client.SendAsync(
            Get($"/api/sections/{sectionBId}/enrollments?academicYearId={yearId}", cookies)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(0, oldList.GetArrayLength());
        Assert.Equal(1, newList.GetArrayLength());
    }

    [Fact]
    public async Task Transfer_ToDifferentGrade_GradeIdChangesInResponse()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (gradeId5, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-T5-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-T5-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Hank", "Gray");

        // Create Grade 6 with section 6-A
        var grade6Res = await client.SendAsync(Post("/api/grades", cookies,
            new { name = "Grade 6-T5-" + Guid.NewGuid().ToString("N")[..6], displayOrder = 60 }));
        var grade6 = await grade6Res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId6 = grade6.GetProperty("id").GetString()!;
        var sec6ARes = await client.SendAsync(Post($"/api/grades/{gradeId6}/sections", cookies,
            new { name = "6-A" }));
        var section6AId = (await sec6ARes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        var enrollRes = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        var enrollmentId = (await enrollRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        var response = await client.SendAsync(Put($"/api/enrollments/{enrollmentId}", cookies,
            new { sectionId = section6AId }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(gradeId6, body.GetProperty("gradeId").GetString());
        Assert.NotEqual(gradeId5, body.GetProperty("gradeId").GetString());
    }

    // ─── DELETE /api/enrollments/{id} ─────────────────────────────────────────

    [Fact]
    public async Task Delete_Returns204_AndRemovesFromList()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-D1-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-D1-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Ivan", "Stone");

        var enrollRes = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        var enrollmentId = (await enrollRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        var deleteResponse = await client.SendAsync(Delete($"/api/enrollments/{enrollmentId}", cookies));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var list = await (await client.SendAsync(
            Get($"/api/sections/{sectionAId}/enrollments?academicYearId={yearId}", cookies)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Delete($"/api/enrollments/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenReEnroll_Returns201_NoPhantomUniqueConstraint()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-D3-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-D3-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Jack", "Hill");

        var enrollRes = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        var enrollmentId = (await enrollRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        await client.SendAsync(Delete($"/api/enrollments/{enrollmentId}", cookies));

        var reEnroll = await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        Assert.Equal(HttpStatusCode.Created, reEnroll.StatusCode);
    }

    // ─── GET /api/enrollments/enrolled-ids ───────────────────────────────────

    [Fact]
    public async Task GetEnrolledIds_EmptyYear_ReturnsEmptyArray()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-EI1-" + Guid.NewGuid().ToString("N")[..6]);

        var response = await client.SendAsync(
            Get($"/api/enrollments/enrolled-ids?academicYearId={yearId}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task GetEnrolledIds_AfterEnroll_ReturnsStudentId()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var (_, sectionAId, _) = await SeedGradeWithTwoSectionsAsync(client, cookies,
            "Grade 5-EI2-" + Guid.NewGuid().ToString("N")[..6]);
        var yearId = await SeedAcademicYearAsync(client, cookies, "2025-EI2-" + Guid.NewGuid().ToString("N")[..6]);
        var studentId = await SeedStudentAsync(client, cookies, "Lily", "Adams");

        await client.SendAsync(Post($"/api/sections/{sectionAId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));

        var response = await client.SendAsync(
            Get($"/api/enrollments/enrolled-ids?academicYearId={yearId}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(studentId, body[0].GetString());
    }

    [Fact]
    public async Task GetEnrolledIds_Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/enrollments/enrolled-ids?academicYearId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/sections/{Guid.NewGuid()}/enrollments?academicYearId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeacherRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        const string teacherEmail = "teacher-enroll@demoschool.test";
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
                    DisplayName = "Teacher Enroll",
                    Role = UserRole.Teacher,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginRes);

        var response = await client.SendAsync(
            Get($"/api/sections/{Guid.NewGuid()}/enrollments?academicYearId={Guid.NewGuid()}", teacherCookies));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
