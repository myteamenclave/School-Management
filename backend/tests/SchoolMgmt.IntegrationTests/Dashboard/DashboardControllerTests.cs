using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;
using SchoolMgmt.IntegrationTests.Fakes;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;
using AppDbContext = SchoolMgmt.Infrastructure.Persistence.AppDbContext;

namespace SchoolMgmt.IntegrationTests.Dashboard;

// The container DB is shared across the whole collection, so school-wide rows accumulate
// across tests. Every test here scopes to a FRESHLY-created academic year and asserts only
// year-scoped aggregates, which are deterministic regardless of what other tests leave behind.
[Collection(IntegrationTestCollection.Name)]
public class DashboardControllerTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid DemoSchoolId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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

    // ─── seed helpers (via API, under the demo school) ────────────────────────

    private static async Task<(string yearId, string semesterId)> SeedYearAsync(
        HttpClient client, string cookies, string name, string startDate = "2025-09-01", string endDate = "2026-06-30")
    {
        var res = await client.SendAsync(Post("/api/academic-years", cookies,
            new { name, startDate, endDate }));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return (body.GetProperty("id").GetString()!, body.GetProperty("semesters")[0].GetProperty("id").GetString()!);
    }

    private static async Task<(string gradeId, string sectionId)> SeedSectionAsync(
        HttpClient client, string cookies, string gradeName)
    {
        var gradeRes = await client.SendAsync(Post("/api/grades", cookies, new { name = gradeName, displayOrder = 70 }));
        var grade = await gradeRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gradeId = grade.GetProperty("id").GetString()!;
        var secRes = await client.SendAsync(Post($"/api/grades/{gradeId}/sections", cookies, new { name = "A" }));
        var section = await secRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return (gradeId, section.GetProperty("id").GetString()!);
    }

    private static async Task<string> SeedSubjectAsync(HttpClient client, string cookies, string tag)
    {
        var res = await client.SendAsync(Post("/api/subjects", cookies, new { name = "Math-" + tag, code = "MATH-" + tag }));
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

    private static async Task<string> SeedStudentAsync(HttpClient client, string cookies, string first = "Alice")
    {
        var res = await client.SendAsync(Post("/api/students", cookies, new
        {
            firstName = first,
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

    private AppDbContext CreateSeedContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        return new AppDbContext(options, new FakeTenantProvider(DemoSchoolId), new FakeDateTimeProvider(DateTimeOffset.UtcNow));
    }

    private static async Task<JsonElement> GetOverviewAsync(HttpClient client, string cookies, string? academicYearId = null)
    {
        var url = academicYearId is null ? "/api/dashboard/overview" : $"/api/dashboard/overview?academicYearId={academicYearId}";
        var res = await client.SendAsync(Get(url, cookies));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }

    // ─── auth ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOverview_Unauthenticated_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync("/api/dashboard/overview");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetOverview_AsTeacher_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        await SeedTeacherAsync(client, admin, tag);
        var teacher = await LoginAsync(client, $"teacher-{tag}@demoschool.test", "Passw0rd!");

        var res = await client.SendAsync(Get("/api/dashboard/overview", teacher));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ─── year resolution ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetOverview_UnknownYear_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);

        var res = await client.SendAsync(Get($"/api/dashboard/overview?academicYearId={Guid.NewGuid()}", admin));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetOverview_ExplicitYear_ReturnsThatYearName()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (yearId, _) = await SeedYearAsync(client, admin, "YR-" + tag);

        var overview = await GetOverviewAsync(client, admin, yearId);

        Assert.Equal(yearId, overview.GetProperty("academicYearId").GetString());
        Assert.Equal("YR-" + tag, overview.GetProperty("academicYearName").GetString());
    }

    [Fact]
    public async Task GetOverview_NoYearParam_DefaultsToCurrentYear()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (yearId, _) = await SeedYearAsync(client, admin, "CUR-" + tag);
        var setCurrent = await client.SendAsync(Post($"/api/academic-years/{yearId}/set-current", admin, new { }));
        Assert.Equal(HttpStatusCode.NoContent, setCurrent.StatusCode);

        var overview = await GetOverviewAsync(client, admin); // no academicYearId

        Assert.Equal(yearId, overview.GetProperty("academicYearId").GetString());
    }

    // ─── finance (seeded directly — no payment API exists) ────────────────────

    [Fact]
    public async Task GetOverview_Finance_ComputesBilledCollectedOverdueAndDraftCount()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        // Year spans past + future so a future installment is genuinely NOT overdue (today = 2026-07-19).
        var (yearId, _) = await SeedYearAsync(client, admin, "FIN-" + tag, "2025-09-01", "2026-12-31");
        var (gradeId, _) = await SeedSectionAsync(client, admin, "Grade-FIN-" + tag);
        var studentA = await SeedStudentAsync(client, admin, "PayerA");
        var studentB = await SeedStudentAsync(client, admin, "PayerB");

        var yearGuid = Guid.Parse(yearId);
        var gradeGuid = Guid.Parse(gradeId);

        await using (var db = CreateSeedContext())
        {
            var template = new FeeTemplate { AcademicYearId = yearGuid, GradeId = gradeGuid, Name = "Tpl-" + tag };
            db.FeeTemplates.Add(template);

            // Issued invoice for student A: 400 paid, 300 overdue (past due, unpaid), 300 future (not overdue).
            var issued = new FeeInvoice
            {
                InvoiceCode = "INV-" + tag + "-1",
                StudentId = Guid.Parse(studentA),
                FeeTemplateId = template.Id,
                AcademicYearId = yearGuid,
                TotalAmount = 1000m,
                Status = InvoiceStatus.Issued,
            };
            db.FeeInvoices.Add(issued);
            db.FeeInvoiceInstallments.AddRange(
                new FeeInvoiceInstallment
                {
                    FeeInvoiceId = issued.Id, Name = "I1", Percentage = 40m, Amount = 400m,
                    DueDate = new DateOnly(2025, 10, 15), AmountPaid = 400m,
                    PaidAt = new DateTime(2025, 10, 20, 0, 0, 0, DateTimeKind.Utc),
                    Status = InstallmentStatus.Paid, DisplayOrder = 0,
                },
                new FeeInvoiceInstallment
                {
                    FeeInvoiceId = issued.Id, Name = "I2", Percentage = 30m, Amount = 300m,
                    DueDate = new DateOnly(2025, 11, 15), Status = InstallmentStatus.Pending, DisplayOrder = 1,
                },
                new FeeInvoiceInstallment
                {
                    FeeInvoiceId = issued.Id, Name = "I3", Percentage = 30m, Amount = 300m,
                    DueDate = new DateOnly(2026, 9, 15), Status = InstallmentStatus.Pending, DisplayOrder = 2,
                });

            // Draft invoice for student B — must be EXCLUDED from billed/collected/overdue.
            var draft = new FeeInvoice
            {
                InvoiceCode = "INV-" + tag + "-2",
                StudentId = Guid.Parse(studentB),
                FeeTemplateId = template.Id,
                AcademicYearId = yearGuid,
                TotalAmount = 500m,
                Status = InvoiceStatus.Draft,
            };
            db.FeeInvoices.Add(draft);
            db.FeeInvoiceInstallments.Add(new FeeInvoiceInstallment
            {
                FeeInvoiceId = draft.Id, Name = "D1", Percentage = 100m, Amount = 500m,
                DueDate = new DateOnly(2025, 10, 15), Status = InstallmentStatus.Pending, DisplayOrder = 0,
            });

            await db.SaveChangesAsync();
        }

        var overview = await GetOverviewAsync(client, admin, yearId);
        var finance = overview.GetProperty("finance");

        Assert.Equal(1000m, finance.GetProperty("billed").GetDecimal());
        Assert.Equal(400m, finance.GetProperty("collected").GetDecimal());
        Assert.Equal(600m, finance.GetProperty("outstanding").GetDecimal());
        Assert.Equal(300m, finance.GetProperty("overdue").GetDecimal());   // only the past-due unpaid one
        Assert.Equal(0.4m, finance.GetProperty("collectionRate").GetDecimal());
        Assert.Equal(1, finance.GetProperty("issuedInvoiceCount").GetInt32());
        Assert.Equal(1, finance.GetProperty("draftInvoiceCount").GetInt32());

        // Monthly series spans every month Sep 2025 .. Dec 2026 inclusive = 16 buckets.
        var monthly = overview.GetProperty("financeMonthly");
        Assert.Equal(16, monthly.GetArrayLength());

        JsonElement Point(int year, int month) =>
            monthly.EnumerateArray().Single(p =>
                p.GetProperty("year").GetInt32() == year && p.GetProperty("month").GetInt32() == month);

        Assert.Equal(400m, Point(2025, 10).GetProperty("billed").GetDecimal());
        Assert.Equal(400m, Point(2025, 10).GetProperty("collected").GetDecimal());
        Assert.Equal(300m, Point(2025, 11).GetProperty("billed").GetDecimal());
        Assert.Equal(0m, Point(2025, 11).GetProperty("collected").GetDecimal());
    }

    // ─── enrollment ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOverview_Enrollment_CountsAndBreakdownByGrade()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (yearId, _) = await SeedYearAsync(client, admin, "ENR-" + tag);
        var (gradeId, sectionId) = await SeedSectionAsync(client, admin, "Grade-ENR-" + tag);
        var s1 = await SeedStudentAsync(client, admin, "Ann");
        var s2 = await SeedStudentAsync(client, admin, "Ben");
        await EnrollAsync(client, admin, sectionId, s1, yearId);
        await EnrollAsync(client, admin, sectionId, s2, yearId);

        var enrollment = (await GetOverviewAsync(client, admin, yearId)).GetProperty("enrollment");

        Assert.Equal(2, enrollment.GetProperty("totalEnrolled").GetInt32());

        var grade = enrollment.GetProperty("byGrade").EnumerateArray()
            .Single(g => g.GetProperty("gradeId").GetString() == gradeId);
        Assert.Equal(2, grade.GetProperty("count").GetInt32());

        var active = enrollment.GetProperty("byStatus").EnumerateArray()
            .Single(s => s.GetProperty("status").GetString() == "Active");
        Assert.Equal(2, active.GetProperty("count").GetInt32());
    }

    // ─── teacher coverage ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetOverview_TeacherCoverage_FlagsSectionWithoutTeacher()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (yearId, _) = await SeedYearAsync(client, admin, "TCH-" + tag);
        var (_, sectionId) = await SeedSectionAsync(client, admin, "Grade-TCH-" + tag);
        var student = await SeedStudentAsync(client, admin);
        await EnrollAsync(client, admin, sectionId, student, yearId);

        // Section has an enrolled student but no teacher assigned for this year → a coverage gap.
        var teachers = (await GetOverviewAsync(client, admin, yearId)).GetProperty("teachers");

        Assert.Equal(0, teachers.GetProperty("assignmentCount").GetInt32());
        Assert.Equal(1, teachers.GetProperty("sectionsWithEnrollments").GetInt32());
        Assert.Equal(1, teachers.GetProperty("sectionsWithoutAnyTeacher").GetInt32());
    }

    [Fact]
    public async Task GetOverview_TeacherCoverage_NoGapWhenAssigned()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (yearId, _) = await SeedYearAsync(client, admin, "TCH2-" + tag);
        var (_, sectionId) = await SeedSectionAsync(client, admin, "Grade-TCH2-" + tag);
        var subjectId = await SeedSubjectAsync(client, admin, tag);
        var teacherId = await SeedTeacherAsync(client, admin, tag);
        var student = await SeedStudentAsync(client, admin);
        await EnrollAsync(client, admin, sectionId, student, yearId);
        await AssignTeacherAsync(client, admin, teacherId, subjectId, sectionId, yearId);

        var teachers = (await GetOverviewAsync(client, admin, yearId)).GetProperty("teachers");

        Assert.Equal(1, teachers.GetProperty("assignmentCount").GetInt32());
        Assert.Equal(0, teachers.GetProperty("sectionsWithoutAnyTeacher").GetInt32());
    }

    // ─── attendance ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOverview_Attendance_MonthlyPresentRate()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var admin = await LoginAsAdminAsync(client);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (yearId, _) = await SeedYearAsync(client, admin, "ATT-" + tag);
        var (_, sectionId) = await SeedSectionAsync(client, admin, "Grade-ATT-" + tag);
        var subjectId = await SeedSubjectAsync(client, admin, tag);
        var teacherId = await SeedTeacherAsync(client, admin, tag);
        var student = await SeedStudentAsync(client, admin);
        await EnrollAsync(client, admin, sectionId, student, yearId);
        await AssignTeacherAsync(client, admin, teacherId, subjectId, sectionId, yearId);

        var teacher = await LoginAsync(client, $"teacher-{tag}@demoschool.test", "Passw0rd!");
        var mark = await client.SendAsync(Put("/api/attendance/bulk", teacher, new
        {
            sectionId,
            academicYearId = yearId,
            date = "2025-10-10",
            entries = new[] { new { studentId = student, status = "Present", notes = (string?)null } },
        }));
        Assert.Equal(HttpStatusCode.OK, mark.StatusCode);

        var attendance = (await GetOverviewAsync(client, admin, yearId)).GetProperty("attendanceMonthly");

        var oct = attendance.EnumerateArray().Single(p =>
            p.GetProperty("year").GetInt32() == 2025 && p.GetProperty("month").GetInt32() == 10);
        Assert.Equal(1, oct.GetProperty("totalRecords").GetInt32());
        Assert.Equal(1, oct.GetProperty("presentCount").GetInt32());
        Assert.Equal(1.0, oct.GetProperty("presentRate").GetDouble());
    }
}
