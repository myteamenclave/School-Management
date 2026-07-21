using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.ParentPortal;

// The parent portal is the first parent-facing read surface. These tests exercise the
// real GradebookService against real Postgres, and (most importantly) the authorization
// guard: a parent may only ever read a child linked to them via StudentParent.
[Collection(IntegrationTestCollection.Name)]
public class ParentPortalTests(PostgresContainerFixture fixture)
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

    private static HttpRequestMessage Put(string url, string cookies, object body)
    {
        var msg = new HttpRequestMessage(HttpMethod.Put, url).WithCookies(cookies);
        msg.Content = JsonContent.Create(body);
        return msg;
    }

    private static string Uniq() => Guid.NewGuid().ToString("N")[..8];

    private static async Task<string> SeedStudentAsync(HttpClient client, string cookies,
        string firstName, string lastName, string guardianEmail)
    {
        var res = await client.SendAsync(Post("/api/students", cookies, new
        {
            firstName,
            lastName,
            dateOfBirth = "2010-03-15",
            gender = "Male",
            enrollmentDate = "2025-09-01",
            guardianName = "Guardian Name",
            guardianEmail,
        }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    // Creates an academic year and marks it current (the fresh test DB seeds no years).
    // Returns the year id.
    private static async Task<string> SeedCurrentYearAsync(HttpClient client, string adminCookies, string name)
    {
        var create = await client.SendAsync(Post("/api/academic-years", adminCookies,
            new { name, startDate = "2025-09-01", endDate = "2026-06-30" }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var yearId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;

        var setCurrent = await client.SendAsync(Post($"/api/academic-years/{yearId}/set-current", adminCookies, new { }));
        Assert.Equal(HttpStatusCode.NoContent, setCurrent.StatusCode);
        return yearId;
    }

    // Creates a parent login for the student (admin), then logs in as that parent and
    // returns the parent's cookie header + user id.
    private static async Task<(string cookies, string parentUserId)> SeedParentForStudentAsync(
        HttpClient client, string adminCookies, string studentId, string email, string password)
    {
        var create = await client.SendAsync(Post($"/api/students/{studentId}/parent-login", adminCookies,
            new { temporaryPassword = password }));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var parentUserId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("parentUserId").GetString()!;

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (CookieTestHelpers.BuildCookieHeader(login), parentUserId);
    }

    [Fact]
    public async Task GetChildren_ListsOnlyOwnLinkedChildren()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        var emailA = $"parent-{Uniq()}@demoschool.test";
        var childA = await SeedStudentAsync(client, admin, "Anna", "Aardvark", emailA);
        // A second family the first parent must NOT see.
        var emailB = $"parent-{Uniq()}@demoschool.test";
        var childB = await SeedStudentAsync(client, admin, "Ben", "Bear", emailB);
        await SeedParentForStudentAsync(client, admin, childB, emailB, "Passw0rd!");

        var (parentA, _) = await SeedParentForStudentAsync(client, admin, childA, emailA, "Passw0rd!");

        var res = await client.SendAsync(Get("/api/parent/children", parentA));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(childA, body[0].GetProperty("studentId").GetString());
        Assert.Equal("Anna Aardvark", body[0].GetProperty("studentName").GetString());
        Assert.NotEqual(childB, body[0].GetProperty("studentId").GetString());
    }

    [Fact]
    public async Task GetChildGrades_LinkedChild_Returns200Array()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        await SeedCurrentYearAsync(client, admin, $"Year-{Uniq()}");
        var email = $"parent-{Uniq()}@demoschool.test";
        var child = await SeedStudentAsync(client, admin, "Cara", "Cat", email);
        var (parent, _) = await SeedParentForStudentAsync(client, admin, child, email, "Passw0rd!");

        // No academicYearId => defaults to the current year. No grades entered yet => empty array.
        var res = await client.SendAsync(Get($"/api/parent/children/{child}/grades", parent));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task GetChildGrades_ChildOfAnotherParent_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        var emailA = $"parent-{Uniq()}@demoschool.test";
        var childA = await SeedStudentAsync(client, admin, "Dana", "Deer", emailA);
        var (parentA, _) = await SeedParentForStudentAsync(client, admin, childA, emailA, "Passw0rd!");

        var emailB = $"parent-{Uniq()}@demoschool.test";
        var childB = await SeedStudentAsync(client, admin, "Evan", "Elk", emailB);
        await SeedParentForStudentAsync(client, admin, childB, emailB, "Passw0rd!");

        // Parent A asks for Parent B's child => 404 (never leak existence).
        var res = await client.SendAsync(Get($"/api/parent/children/{childB}/grades", parentA));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetChildGrades_UnknownChild_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        var email = $"parent-{Uniq()}@demoschool.test";
        var child = await SeedStudentAsync(client, admin, "Fin", "Fox", email);
        var (parent, _) = await SeedParentForStudentAsync(client, admin, child, email, "Passw0rd!");

        var res = await client.SendAsync(Get($"/api/parent/children/{Guid.NewGuid()}/grades", parent));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetAcademicYears_Returns200_WithCurrentFlag()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        await SeedCurrentYearAsync(client, admin, $"Year-{Uniq()}");
        var email = $"parent-{Uniq()}@demoschool.test";
        var child = await SeedStudentAsync(client, admin, "Gus", "Goat", email);
        var (parent, _) = await SeedParentForStudentAsync(client, admin, child, email, "Passw0rd!");

        var res = await client.SendAsync(Get("/api/parent/academic-years", parent));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() >= 1);
        Assert.Contains(body.EnumerateArray(), y => y.GetProperty("isCurrent").GetBoolean());
    }

    // ─── Attendance ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetChildAttendance_LinkedChild_ReturnsSummaryAndLog()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Uniq();

        // Full scaffold: grade → section, current year, subject, teacher, student, enrol, assign.
        var gradeId = (await (await client.SendAsync(Post("/api/grades", admin,
            new { name = "Grade-" + tag, displayOrder = 80 })))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;
        var sectionId = (await (await client.SendAsync(Post($"/api/grades/{gradeId}/sections", admin, new { name = "A" })))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;
        var yearId = await SeedCurrentYearAsync(client, admin, "ATT-" + tag);
        var subjectId = (await (await client.SendAsync(Post("/api/subjects", admin,
            new { name = "Math-" + tag, code = "MATH-" + tag })))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;

        var teacherEmail = $"teacher-{tag}@demoschool.test";
        var teacherId = (await (await client.SendAsync(Post("/api/teachers", admin, new
        {
            email = teacherEmail,
            password = "Passw0rd!",
            firstName = "Test",
            lastName = "Teacher",
            phone = "555-0200",
            joiningDate = "2025-09-01",
        }))).Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;

        var email = $"parent-{tag}@demoschool.test";
        var child = await SeedStudentAsync(client, admin, "Holly", "Hen", email);

        await client.SendAsync(Post($"/api/sections/{sectionId}/enrollments", admin,
            new { studentId = child, academicYearId = yearId }));
        await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", admin,
            new { subjectId, sectionId, academicYearId = yearId }));

        var (parent, _) = await SeedParentForStudentAsync(client, admin, child, email, "Passw0rd!");

        // Teacher marks three days: Present, Late, Absent (one status per student per date).
        var teacher = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = "Passw0rd!" });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(teacher);

        async Task Mark(string date, string status) =>
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Put("/api/attendance/bulk", teacherCookies, new
            {
                sectionId,
                academicYearId = yearId,
                date,
                entries = new[] { new { studentId = child, status, notes = (string?)null } },
            }))).StatusCode);

        await Mark("2025-09-01", "Present");
        await Mark("2025-09-02", "Late");
        await Mark("2025-09-03", "Absent");

        var res = await client.SendAsync(Get($"/api/parent/children/{child}/attendance", parent));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var summary = body.GetProperty("summary");
        Assert.Equal(3, summary.GetProperty("totalMarked").GetInt32());
        Assert.Equal(1, summary.GetProperty("presentCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("lateCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("absentCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("excusedCount").GetInt32());
        // (1 Present + 1 Late) / 3 = 66.7
        Assert.Equal(66.7m, summary.GetProperty("attendanceRate").GetDecimal());
        Assert.Equal(3, body.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task GetChildAttendance_ChildOfAnotherParent_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        var emailA = $"parent-{Uniq()}@demoschool.test";
        var childA = await SeedStudentAsync(client, admin, "Ivy", "Ibis", emailA);
        var (parentA, _) = await SeedParentForStudentAsync(client, admin, childA, emailA, "Passw0rd!");

        var emailB = $"parent-{Uniq()}@demoschool.test";
        var childB = await SeedStudentAsync(client, admin, "Jack", "Jay", emailB);
        await SeedParentForStudentAsync(client, admin, childB, emailB, "Passw0rd!");

        var res = await client.SendAsync(Get($"/api/parent/children/{childB}/attendance", parentA));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetChildAttendance_LinkedChildNoRecords_ReturnsEmptySummary()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        await SeedCurrentYearAsync(client, admin, $"Year-{Uniq()}");
        var email = $"parent-{Uniq()}@demoschool.test";
        var child = await SeedStudentAsync(client, admin, "Kim", "Koala", email);
        var (parent, _) = await SeedParentForStudentAsync(client, admin, child, email, "Passw0rd!");

        var res = await client.SendAsync(Get($"/api/parent/children/{child}/attendance", parent));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, body.GetProperty("summary").GetProperty("totalMarked").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("summary").GetProperty("attendanceRate").ValueKind);
        Assert.Equal(0, body.GetProperty("entries").GetArrayLength());
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync("/api/parent/children");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task AdminRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        // Admin is not a Parent — the parent portal is Parent-only.
        var res = await client.SendAsync(Get("/api/parent/children", admin));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
