using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Attendance;

// Guards on the teacher bulk-upsert endpoint (spec 16 fix B — reject dates outside the year window).
[Collection(IntegrationTestCollection.Name)]
public class AttendanceMarkingTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    // Full scaffold under a year running 2025-09-01 .. 2026-06-30.
    private static async Task<(string sectionId, string yearId, string studentId, string teacherEmail)>
        SeedScenarioAsync(HttpClient client, string admin, string tag)
    {
        var gradeRes = await client.SendAsync(Post("/api/grades", admin, new { name = "Grade-" + tag, displayOrder = 70 }));
        var gradeId = (await gradeRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;
        var secRes = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", admin, new { name = "A" }));
        var sectionId = (await secRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;

        var yearRes = await client.SendAsync(Post("/api/academic-years", admin,
            new { name = "ATT-" + tag, startDate = "2025-09-01", endDate = "2026-06-30" }));
        var yearId = (await yearRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;

        var subjRes = await client.SendAsync(Post("/api/subjects", admin, new { name = "Math-" + tag, code = "MATH-" + tag }));
        var subjectId = (await subjRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;

        var teacherRes = await client.SendAsync(Post("/api/teachers", admin, new
        {
            email = $"teacher-{tag}@demoschool.test",
            password = "Passw0rd!",
            firstName = "Test",
            lastName = "Teacher",
            phone = "555-0200",
            joiningDate = "2025-09-01",
        }));
        var teacherId = (await teacherRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;

        var studentRes = await client.SendAsync(Post("/api/students", admin, new
        {
            firstName = "Alice",
            lastName = "Chen",
            dateOfBirth = "2012-05-15",
            gender = "Female",
            enrollmentDate = "2025-09-01",
        }));
        var studentId = (await studentRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;

        await client.SendAsync(Post($"/api/sections/{sectionId}/enrollments", admin, new { studentId, academicYearId = yearId }));
        await client.SendAsync(Post($"/api/teachers/{teacherId}/assignments", admin, new { subjectId, sectionId, academicYearId = yearId }));

        return (sectionId, yearId, studentId, $"teacher-{tag}@demoschool.test");
    }

    [Fact]
    public async Task BulkUpsert_DateOutsideYearWindow_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionId, yearId, studentId, teacherEmail) = await SeedScenarioAsync(client, admin, tag);
        var teacher = await LoginAsync(client, teacherEmail, "Passw0rd!");

        // 2026-07-20 is after the year's EndDate (2026-06-30) — the exact real-world case.
        var res = await client.SendAsync(Put("/api/attendance/bulk", teacher, new
        {
            sectionId,
            academicYearId = yearId,
            date = "2026-07-20",
            entries = new[] { new { studentId, status = "Present", notes = (string?)null } },
        }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task BulkUpsert_DateInsideYearWindow_Succeeds()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (sectionId, yearId, studentId, teacherEmail) = await SeedScenarioAsync(client, admin, tag);
        var teacher = await LoginAsync(client, teacherEmail, "Passw0rd!");

        var res = await client.SendAsync(Put("/api/attendance/bulk", teacher, new
        {
            sectionId,
            academicYearId = yearId,
            date = "2025-10-10",
            entries = new[] { new { studentId, status = "Present", notes = (string?)null } },
        }));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
