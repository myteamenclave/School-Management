using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Gradebook;

[Collection(IntegrationTestCollection.Name)]
public class GradebookControllerTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ─── request helpers ──────────────────────────────────────────────────────

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

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return CookieTestHelpers.BuildCookieHeader(res);
    }

    private static Task<string> LoginAsAdminAsync(HttpClient client) =>
        LoginAsync(client, "admin@demoschool.test", "Passw0rd!");

    // ─── seed helpers ─────────────────────────────────────────────────────────

    private static async Task<string> SeedSectionAsync(HttpClient client, string cookies, string gradeName)
    {
        var gradeRes = await client.SendAsync(Post("/api/grades", cookies,
            new { name = gradeName, displayOrder = 70 }));
        var grade = await gradeRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString()!;

        var secRes = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies, new { name = "5-A" }));
        var section = await secRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return section.GetProperty("id").GetString()!;
    }

    // Returns (yearId, semesterId) — semesterId is Semester 1 of the created year.
    private static async Task<(string yearId, string semesterId)> SeedYearAsync(
        HttpClient client, string cookies, string name)
    {
        var res = await client.SendAsync(Post("/api/academic-years", cookies,
            new { name, startDate = "2025-09-01", endDate = "2026-06-30" }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearId = body.GetProperty("id").GetString()!;
        var semesterId = body.GetProperty("semesters")[0].GetProperty("id").GetString()!;
        return (yearId, semesterId);
    }

    private static async Task<string> SeedSubjectAsync(HttpClient client, string cookies, string tag)
    {
        var res = await client.SendAsync(Post("/api/subjects", cookies,
            new { name = "Math-" + tag, code = "MATH-" + tag }));
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    // Returns the teacher's domain id; login creds are teacher-{suffix}@demoschool.test / Passw0rd!.
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

    private static async Task<string> SeedStudentAsync(HttpClient client, string cookies)
    {
        var res = await client.SendAsync(Post("/api/students", cookies, new
        {
            firstName = "Alice",
            lastName = "Chen",
            dateOfBirth = "2012-05-15",
            gender = "Female",
            enrollmentDate = "2025-09-01",
            guardianName = "Bob Chen",
            guardianPhone = "555-0100",
            guardianEmail = "bob.chen@example.com",
        }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetString()!;
    }

    private static async Task EnrollAsync(HttpClient client, string cookies, string sectionId, string studentId, string yearId)
    {
        var res = await client.SendAsync(Post($"/api/sections/{sectionId}/enrollments", cookies,
            new { studentId, academicYearId = yearId }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    private static async Task AssignTeacherAsync(
        HttpClient client, string cookies, string teacherId, string subjectId, string sectionId, string yearId)
    {
        var res = await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", cookies,
            new { subjectId, sectionId, academicYearId = yearId }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    // Full happy-path scaffold: section, year, subject, teacher (assigned), student (enrolled).
    private async Task<(string sectionId, string subjectId, string semesterId, string studentId, string teacherEmail)>
        SeedFullScenarioAsync(HttpClient client, string adminCookies, string tag)
    {
        var sectionId = await SeedSectionAsync(client, adminCookies, "Grade-" + tag);
        var (yearId, semesterId) = await SeedYearAsync(client, adminCookies, "2025-" + tag);
        var subjectId = await SeedSubjectAsync(client, adminCookies, tag);
        var teacherId = await SeedTeacherAsync(client, adminCookies, tag);
        var studentId = await SeedStudentAsync(client, adminCookies);
        await EnrollAsync(client, adminCookies, sectionId, studentId, yearId);
        await AssignTeacherAsync(client, adminCookies, teacherId, subjectId, sectionId, yearId);
        return (sectionId, subjectId, semesterId, studentId, $"teacher-{tag}@demoschool.test");
    }

    // ─── PUT /api/gradebook/bulk ──────────────────────────────────────────────

    [Fact]
    public async Task BulkUpsert_AssignedTeacher_AllComponents_ComputesTermAndLetter()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionId, subjectId, semesterId, studentId, teacherEmail) =
            await SeedFullScenarioAsync(client, admin, tag);
        var teacher = await LoginAsync(client, teacherEmail, "Passw0rd!");

        var upsert = await client.SendAsync(Put("/api/gradebook/bulk", teacher, new
        {
            sectionId,
            subjectId,
            semesterId,
            entries = new[] { new { studentId, midterm = 85m, final = 90m, coursework = 80m, notes = (string?)null } },
        }));
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var roster = await (await client.SendAsync(Get(
            $"/api/gradebook/subject-roster?sectionId={sectionId}&subjectId={subjectId}&semesterId={semesterId}", teacher)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var entry = roster.GetProperty("entries")[0];
        // 85*0.30 + 90*0.40 + 80*0.30 = 85.5 → band B (80–89.99)
        Assert.Equal(85.5m, entry.GetProperty("termScore").GetDecimal());
        Assert.Equal("B", entry.GetProperty("letterGrade").GetString());
    }

    [Fact]
    public async Task BulkUpsert_PartialComponents_LeavesTermScoreNull()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionId, subjectId, semesterId, studentId, teacherEmail) =
            await SeedFullScenarioAsync(client, admin, tag);
        var teacher = await LoginAsync(client, teacherEmail, "Passw0rd!");

        await client.SendAsync(Put("/api/gradebook/bulk", teacher, new
        {
            sectionId,
            subjectId,
            semesterId,
            entries = new[] { new { studentId, midterm = 85m, final = (decimal?)null, coursework = (decimal?)null, notes = (string?)null } },
        }));

        var roster = await (await client.SendAsync(Get(
            $"/api/gradebook/subject-roster?sectionId={sectionId}&subjectId={subjectId}&semesterId={semesterId}", teacher)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var entry = roster.GetProperty("entries")[0];
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("termScore").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("letterGrade").ValueKind);
        Assert.Equal(85m, entry.GetProperty("midtermScore").GetDecimal());
    }

    [Fact]
    public async Task BulkUpsert_Reupsert_UpdatesInPlace()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionId, subjectId, semesterId, studentId, teacherEmail) =
            await SeedFullScenarioAsync(client, admin, tag);
        var teacher = await LoginAsync(client, teacherEmail, "Passw0rd!");

        object Body(decimal mid) => new
        {
            sectionId,
            subjectId,
            semesterId,
            entries = new[] { new { studentId, midterm = mid, final = 90m, coursework = 80m, notes = (string?)null } },
        };

        await client.SendAsync(Put("/api/gradebook/bulk", teacher, Body(50m)));
        await client.SendAsync(Put("/api/gradebook/bulk", teacher, Body(100m)));

        var roster = await (await client.SendAsync(Get(
            $"/api/gradebook/subject-roster?sectionId={sectionId}&subjectId={subjectId}&semesterId={semesterId}", teacher)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(1, roster.GetProperty("entries").GetArrayLength());
        // 100*0.30 + 90*0.40 + 80*0.30 = 90 → band A
        Assert.Equal(90m, roster.GetProperty("entries")[0].GetProperty("termScore").GetDecimal());
        Assert.Equal("A", roster.GetProperty("entries")[0].GetProperty("letterGrade").GetString());
    }

    [Fact]
    public async Task BulkUpsert_UnassignedTeacher_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        // Scaffold everything EXCEPT the teacher assignment.
        var sectionId = await SeedSectionAsync(client, admin, "Grade-" + tag);
        var (yearId, semesterId) = await SeedYearAsync(client, admin, "2025-" + tag);
        var subjectId = await SeedSubjectAsync(client, admin, tag);
        await SeedTeacherAsync(client, admin, tag);
        var studentId = await SeedStudentAsync(client, admin);
        await EnrollAsync(client, admin, sectionId, studentId, yearId);
        var teacher = await LoginAsync(client, $"teacher-{tag}@demoschool.test", "Passw0rd!");

        var res = await client.SendAsync(Put("/api/gradebook/bulk", teacher, new
        {
            sectionId,
            subjectId,
            semesterId,
            entries = new[] { new { studentId, midterm = 85m, final = 90m, coursework = 80m, notes = (string?)null } },
        }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task BulkUpsert_ArchivedYear_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var sectionId = await SeedSectionAsync(client, admin, "Grade-" + tag);
        var (yearId, semesterId) = await SeedYearAsync(client, admin, "2025-" + tag);
        var subjectId = await SeedSubjectAsync(client, admin, tag);
        var teacherId = await SeedTeacherAsync(client, admin, tag);
        var studentId = await SeedStudentAsync(client, admin);
        await EnrollAsync(client, admin, sectionId, studentId, yearId);
        await AssignTeacherAsync(client, admin, teacherId, subjectId, sectionId, yearId);

        // Archive the year (it is not current, so archiving is allowed).
        var archive = await client.SendAsync(Post($"/api/academic-years/{yearId}/archive", admin, new { }));
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var teacher = await LoginAsync(client, $"teacher-{tag}@demoschool.test", "Passw0rd!");
        var res = await client.SendAsync(Put("/api/gradebook/bulk", teacher, new
        {
            sectionId,
            subjectId,
            semesterId,
            entries = new[] { new { studentId, midterm = 85m, final = 90m, coursework = 80m, notes = (string?)null } },
        }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task BulkUpsert_ByAdmin_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionId, subjectId, semesterId, studentId, _) = await SeedFullScenarioAsync(client, admin, tag);

        // Admin is not permitted to call the teacher-only bulk endpoint.
        var res = await client.SendAsync(Put("/api/gradebook/bulk", admin, new
        {
            sectionId,
            subjectId,
            semesterId,
            entries = new[] { new { studentId, midterm = 85m, final = 90m, coursework = 80m, notes = (string?)null } },
        }));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task BulkUpsert_Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var res = await client.PutAsJsonAsync("/api/gradebook/bulk", new
        {
            sectionId = Guid.NewGuid(),
            subjectId = Guid.NewGuid(),
            semesterId = Guid.NewGuid(),
            entries = new[] { new { studentId = Guid.NewGuid(), midterm = 85m, final = 90m, coursework = 80m, notes = (string?)null } },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ─── GET /api/gradebook/subject-roster ────────────────────────────────────

    [Fact]
    public async Task GetSubjectRoster_BeforeAnyGrades_ListsEnrolledStudentsWithNullScores()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionId, subjectId, semesterId, _, _) = await SeedFullScenarioAsync(client, admin, tag);

        var roster = await (await client.SendAsync(Get(
            $"/api/gradebook/subject-roster?sectionId={sectionId}&subjectId={subjectId}&semesterId={semesterId}", admin)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(1, roster.GetProperty("entries").GetArrayLength());
        var entry = roster.GetProperty("entries")[0];
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("midtermScore").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("termScore").ValueKind);
    }

    // ─── GET /api/gradebook/student ───────────────────────────────────────────

    [Fact]
    public async Task GetStudentGrades_AfterUpsert_ReturnsGrade()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionId, subjectId, semesterId, studentId, teacherEmail) =
            await SeedFullScenarioAsync(client, admin, tag);
        // Need the year id for the student query — re-derive from the roster is not possible,
        // so recreate the scenario returns semesterId only; fetch grades by the known year via roster.
        var teacher = await LoginAsync(client, teacherEmail, "Passw0rd!");
        await client.SendAsync(Put("/api/gradebook/bulk", teacher, new
        {
            sectionId,
            subjectId,
            semesterId,
            entries = new[] { new { studentId, midterm = 70m, final = 70m, coursework = 70m, notes = "solid" } },
        }));

        // academicYearId is discoverable from the years list (single seeded scenario year matching tag).
        var years = await (await client.SendAsync(Get("/api/academic-years", admin)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        string? yearId = null;
        foreach (var y in years.EnumerateArray())
            if (y.GetProperty("name").GetString() == "2025-" + tag)
                yearId = y.GetProperty("id").GetString();
        Assert.NotNull(yearId);

        var grades = await (await client.SendAsync(Get(
            $"/api/gradebook/student?studentId={studentId}&academicYearId={yearId}", admin)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(1, grades.GetArrayLength());
        Assert.Equal(70m, grades[0].GetProperty("termScore").GetDecimal()); // 70 across all → 70
        Assert.Equal("C", grades[0].GetProperty("letterGrade").GetString()); // 70 → band C (70–79.99)
    }

    [Fact]
    public async Task GetSubjectRoster_Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync(
            $"/api/gradebook/subject-roster?sectionId={Guid.NewGuid()}&subjectId={Guid.NewGuid()}&semesterId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
