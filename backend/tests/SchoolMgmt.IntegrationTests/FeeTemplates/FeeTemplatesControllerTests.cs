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

namespace SchoolMgmt.IntegrationTests.FeeTemplates;

[Collection(IntegrationTestCollection.Name)]
public class FeeTemplatesControllerTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ─── Helpers ─────────────────────────────────────────────────────────────

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

    // Creates a seeded AcademicYear and two Grades; returns (academicYearId, grade5Id, kgGradeId)
    private static async Task<(string AcademicYearId, string Grade5Id, string KgId)> SeedAsync(
        HttpClient client, string cookies)
    {
        var yearResp = await client.SendAsync(Post("/api/academic-years", cookies, new
        {
            name = $"AY-{Guid.NewGuid():N}",
            startDate = "2025-06-01",
            endDate = "2026-03-31",
        }));
        var year = await yearResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var yearId = year.GetProperty("id").GetString()!;

        var g5Resp = await client.SendAsync(Post("/api/grades", cookies, new
        {
            name = $"Grade5-{Guid.NewGuid():N}",
            displayOrder = 5,
        }));
        var g5 = await g5Resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var g5Id = g5.GetProperty("id").GetString()!;

        var kgResp = await client.SendAsync(Post("/api/grades", cookies, new
        {
            name = $"KG-{Guid.NewGuid():N}",
            displayOrder = 1,
        }));
        var kg = await kgResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var kgId = kg.GetProperty("id").GetString()!;

        return (yearId, g5Id, kgId);
    }

    private static async Task<JsonElement> CreateTemplateAsync(
        HttpClient client, string cookies, string yearId, string gradeId, string name = "Standard")
    {
        var resp = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name,
            academicYearId = yearId,
            gradeId,
        }));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }

    // ─── POST /api/fee-templates ──────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201WithEmptyChildren()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var response = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name = "Standard",
            academicYearId = yearId,
            gradeId = g5Id,
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Standard", body.GetProperty("name").GetString());
        Assert.Equal(0, body.GetProperty("lineItems").GetArrayLength());
        Assert.Equal(0, body.GetProperty("installments").GetArrayLength());
        Assert.Equal(0, body.GetProperty("discountRules").GetArrayLength());
        Assert.Equal(0m, body.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task Create_MissingName_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var response = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name = "",
            academicYearId = yearId,
            gradeId = g5Id,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NonExistentAcademicYear_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (_, g5Id, _) = await SeedAsync(client, cookies);

        var response = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name = "Standard",
            academicYearId = Guid.NewGuid(),
            gradeId = g5Id,
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_NonExistentGrade_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, _, _) = await SeedAsync(client, cookies);

        var response = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name = "Standard",
            academicYearId = yearId,
            gradeId = Guid.NewGuid(),
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateNameSameGradeAndYear_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        await CreateTemplateAsync(client, cookies, yearId, g5Id, "Dup");
        var response = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name = "Dup",
            academicYearId = yearId,
            gradeId = g5Id,
        }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameNameDifferentGrade_Returns201()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, kgId) = await SeedAsync(client, cookies);

        await CreateTemplateAsync(client, cookies, yearId, g5Id, "Standard");
        var response = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name = "Standard",
            academicYearId = yearId,
            gradeId = kgId,
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_MultipleTemplatesSameGradeAndYear_BothSucceed()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var r1 = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name = "Standard",
            academicYearId = yearId,
            gradeId = g5Id,
        }));
        var r2 = await client.SendAsync(Post("/api/fee-templates", cookies, new
        {
            name = "Merit Scholarship",
            academicYearId = yearId,
            gradeId = g5Id,
        }));

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
    }

    // ─── GET /api/fee-templates ───────────────────────────────────────────────

    [Fact]
    public async Task GetAll_NoParams_ReturnsOnlyActiveTemplates()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id, "Active-GetAll");

        // Deactivate it
        var id = template.GetProperty("id").GetString();
        await client.SendAsync(Put($"/api/fee-templates/{id}", cookies, new { name = "Active-GetAll", isActive = false }));

        var response = await client.SendAsync(Get($"/api/fee-templates?gradeId={g5Id}", cookies));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.DoesNotContain(items, i => i.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task GetAll_IsActiveFalse_ReturnsOnlyInactive()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id, "Inactive-Filter");
        var id = template.GetProperty("id").GetString();
        await client.SendAsync(Put($"/api/fee-templates/{id}", cookies, new { name = "Inactive-Filter", isActive = false }));

        var response = await client.SendAsync(Get($"/api/fee-templates?isActive=false&gradeId={g5Id}", cookies));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task GetAll_FilterByGradeId_ReturnsOnlyThatGrade()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, kgId) = await SeedAsync(client, cookies);

        var g5Template = await CreateTemplateAsync(client, cookies, yearId, g5Id, "G5-Only");
        await CreateTemplateAsync(client, cookies, yearId, kgId, "KG-Only");

        var response = await client.SendAsync(Get($"/api/fee-templates?gradeId={g5Id}", cookies));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("id").GetString() == g5Template.GetProperty("id").GetString());
        Assert.All(items, i => Assert.Equal(g5Id, i.GetProperty("gradeId").GetString()));
    }

    [Fact]
    public async Task GetAll_FilterByAcademicYearId_FiltersCorrectly()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        await CreateTemplateAsync(client, cookies, yearId, g5Id, "YearFilter");

        var response = await client.SendAsync(Get($"/api/fee-templates?academicYearId={yearId}", cookies));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(yearId, i.GetProperty("academicYearId").GetString()));
    }

    [Fact]
    public async Task GetAll_Pagination_ReturnsPaginationFields()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        await CreateTemplateAsync(client, cookies, yearId, g5Id, "Page-A");
        await CreateTemplateAsync(client, cookies, yearId, g5Id, "Page-B");

        var response = await client.SendAsync(Get($"/api/fee-templates?academicYearId={yearId}&gradeId={g5Id}&page=1&pageSize=5", cookies));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.True(body.GetProperty("totalCount").GetInt32() >= 2);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(5, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetAll_TotalAmountIsZeroForEmptyTemplate()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        await CreateTemplateAsync(client, cookies, yearId, g5Id, "ZeroAmount");

        var response = await client.SendAsync(Get($"/api/fee-templates?academicYearId={yearId}&gradeId={g5Id}", cookies));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        var item = items.First(i => i.GetProperty("name").GetString() == "ZeroAmount");

        Assert.Equal(0m, item.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(0, item.GetProperty("lineItemCount").GetInt32());
    }

    // ─── GET /api/fee-templates/{id} ─────────────────────────────────────────

    [Fact]
    public async Task GetById_Returns200WithEmptyChildren()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Get($"/api/fee-templates/{id}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal(0, body.GetProperty("lineItems").GetArrayLength());
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Get($"/api/fee-templates/{Guid.NewGuid()}", cookies));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/fee-templates/{id} ─────────────────────────────────────────

    [Fact]
    public async Task UpdateHeader_NameAndIsActive_Returns200()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id, "OriginalName");
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}", cookies,
            new { name = "UpdatedName", isActive = false }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("UpdatedName", body.GetProperty("name").GetString());
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task UpdateHeader_DeactivatedTemplate_DisappearsFromDefaultList()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id, "ToDeactivate");
        var id = template.GetProperty("id").GetString();

        await client.SendAsync(Put($"/api/fee-templates/{id}", cookies, new { name = "ToDeactivate", isActive = false }));

        var listResp = await client.SendAsync(Get($"/api/fee-templates?gradeId={g5Id}", cookies));
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var ids = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).ToList();

        Assert.DoesNotContain(id, ids);
    }

    [Fact]
    public async Task UpdateHeader_DuplicateName_Returns409()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        await CreateTemplateAsync(client, cookies, yearId, g5Id, "NameA");
        var b = await CreateTemplateAsync(client, cookies, yearId, g5Id, "NameB");
        var bId = b.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{bId}", cookies,
            new { name = "NameA", isActive = true }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateHeader_UnknownId_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Put($"/api/fee-templates/{Guid.NewGuid()}", cookies,
            new { name = "Any", isActive = true }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/fee-templates/{id}/line-items ───────────────────────────────

    [Fact]
    public async Task ReplaceLineItems_ThreeItems_Returns200WithCorrectTotals()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[]
            {
                new { name = "Tuition Fee", amount = 35000m, displayOrder = 1 },
                new { name = "Activity Fee", amount = 4000m, displayOrder = 2 },
                new { name = "Lab Fee", amount = 3000m, displayOrder = 3 },
            }
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(3, body.GetProperty("lineItems").GetArrayLength());
        Assert.Equal(42000m, body.GetProperty("totalAmount").GetDecimal());

        // Ordered by displayOrder
        var items = body.GetProperty("lineItems").EnumerateArray().ToList();
        Assert.Equal("Tuition Fee", items[0].GetProperty("name").GetString());
        Assert.Equal("Activity Fee", items[1].GetProperty("name").GetString());
        Assert.Equal("Lab Fee", items[2].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ReplaceLineItems_ResendWithIds_UpdatesInPlace()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        // Create 2 items
        var first = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[]
            {
                new { name = "Tuition Fee", amount = 35000m, displayOrder = 1 },
                new { name = "Activity Fee", amount = 4000m, displayOrder = 2 },
            }
        }));
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var tuitionId = firstBody.GetProperty("lineItems")[0].GetProperty("id").GetString();
        var activityId = firstBody.GetProperty("lineItems")[1].GetProperty("id").GetString();

        // Resend with same IDs but updated amounts
        var second = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[]
            {
                new { id = tuitionId, name = "Tuition Fee", amount = 35000m, displayOrder = 1 },
                new { id = activityId, name = "Activity Fee", amount = 4500m, displayOrder = 2 },
            }
        }));
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var updatedIds = secondBody.GetProperty("lineItems").EnumerateArray()
            .Select(li => li.GetProperty("id").GetString()).ToList();

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, updatedIds.Count);
        Assert.Contains(tuitionId, updatedIds);
        Assert.Contains(activityId, updatedIds);
        Assert.Equal(39500m, secondBody.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task ReplaceLineItems_OmitOneItem_ThatItemIsRemoved()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        // Create 3 items
        var initial = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[]
            {
                new { name = "Tuition Fee", amount = 35000m, displayOrder = 1 },
                new { name = "Activity Fee", amount = 4000m, displayOrder = 2 },
                new { name = "Lab Fee", amount = 3000m, displayOrder = 3 },
            }
        }));
        var initBody = await initial.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var tuitionId = initBody.GetProperty("lineItems")[0].GetProperty("id").GetString();
        var activityId = initBody.GetProperty("lineItems")[1].GetProperty("id").GetString();

        // Omit Lab Fee
        var updated = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[]
            {
                new { id = tuitionId, name = "Tuition Fee", amount = 35000m, displayOrder = 1 },
                new { id = activityId, name = "Activity Fee", amount = 4000m, displayOrder = 2 },
            }
        }));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(2, updatedBody.GetProperty("lineItems").GetArrayLength());
        Assert.Equal(39000m, updatedBody.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task ReplaceLineItems_EmptyList_Returns200WithEmptyItems()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[] { new { name = "Tuition", amount = 1000m, displayOrder = 1 } }
        }));

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = Array.Empty<object>()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, body.GetProperty("lineItems").GetArrayLength());
    }

    [Fact]
    public async Task ReplaceLineItems_NegativeAmount_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[] { new { name = "Bad Item", amount = -100m, displayOrder = 1 } }
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceLineItems_EmptyName_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[] { new { name = "", amount = 100m, displayOrder = 1 } }
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceLineItems_UnknownTemplate_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Put($"/api/fee-templates/{Guid.NewGuid()}/line-items", cookies, new
        {
            items = Array.Empty<object>()
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/fee-templates/{id}/installments ─────────────────────────────

    [Fact]
    public async Task ReplaceInstallments_SummingTo100_Returns200()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/installments", cookies, new
        {
            items = new[]
            {
                new { name = "1st Installment", percentage = 40m, displayOrder = 1 },
                new { name = "2nd Installment", percentage = 30m, displayOrder = 2 },
                new { name = "3rd Installment", percentage = 30m, displayOrder = 3 },
            }
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(3, body.GetProperty("installments").GetArrayLength());
    }

    [Fact]
    public async Task ReplaceInstallments_SummingTo80_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/installments", cookies, new
        {
            items = new[]
            {
                new { name = "1st", percentage = 40m, displayOrder = 1 },
                new { name = "2nd", percentage = 40m, displayOrder = 2 },
            }
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceInstallments_SummingTo110_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/installments", cookies, new
        {
            items = new[]
            {
                new { name = "1st", percentage = 60m, displayOrder = 1 },
                new { name = "2nd", percentage = 50m, displayOrder = 2 },
            }
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceInstallments_SingleEntry100Percent_Returns200()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/installments", cookies, new
        {
            items = new[] { new { name = "Full Payment", percentage = 100m, displayOrder = 1 } }
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetProperty("installments").GetArrayLength());
    }

    [Fact]
    public async Task ReplaceInstallments_EmptyList_Returns200()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/installments", cookies, new
        {
            items = Array.Empty<object>()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, body.GetProperty("installments").GetArrayLength());
    }

    [Fact]
    public async Task ReplaceInstallments_UnknownTemplate_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Put($"/api/fee-templates/{Guid.NewGuid()}/installments", cookies, new
        {
            items = Array.Empty<object>()
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/fee-templates/{id}/discount-rules ───────────────────────────

    [Fact]
    public async Task ReplaceDiscountRules_PercentageRuleNoLineItem_Returns200()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/discount-rules", cookies, new
        {
            items = new[] { new { name = "Early Bird", ruleType = "FlatAmount", value = 500m, feeLineItemId = (string?)null } }
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var rule = body.GetProperty("discountRules")[0];
        Assert.Equal("Early Bird", rule.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, rule.GetProperty("feeLineItemId").ValueKind);
        Assert.Equal(JsonValueKind.Null, rule.GetProperty("feeLineItemName").ValueKind);
    }

    [Fact]
    public async Task ReplaceDiscountRules_TargetingValidLineItem_Returns200WithName()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        // Add a line item first
        var lineResp = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[] { new { name = "Tuition Fee", amount = 35000m, displayOrder = 1 } }
        }));
        var lineBody = await lineResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lineItemId = lineBody.GetProperty("lineItems")[0].GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/discount-rules", cookies, new
        {
            items = new[] { new { name = "Sibling Discount", ruleType = "Percentage", value = 10m, feeLineItemId = lineItemId } }
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var rule = body.GetProperty("discountRules")[0];
        Assert.Equal(lineItemId, rule.GetProperty("feeLineItemId").GetString());
        Assert.Equal("Tuition Fee", rule.GetProperty("feeLineItemName").GetString());
    }

    [Fact]
    public async Task ReplaceDiscountRules_FeeLineItemFromDifferentTemplate_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, kgId) = await SeedAsync(client, cookies);

        // Template A with a line item
        var tA = await CreateTemplateAsync(client, cookies, yearId, g5Id, "TemplateA");
        var tAId = tA.GetProperty("id").GetString();
        var lAResp = await client.SendAsync(Put($"/api/fee-templates/{tAId}/line-items", cookies, new
        {
            items = new[] { new { name = "Tuition", amount = 35000m, displayOrder = 1 } }
        }));
        var lABody = await lAResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lineItemFromA = lABody.GetProperty("lineItems")[0].GetProperty("id").GetString();

        // Template B — try to reference Template A's line item
        var tB = await CreateTemplateAsync(client, cookies, yearId, kgId, "TemplateB");
        var tBId = tB.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{tBId}/discount-rules", cookies, new
        {
            items = new[] { new { name = "Bad Rule", ruleType = "Percentage", value = 10m, feeLineItemId = lineItemFromA } }
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceDiscountRules_PercentageOver100_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/discount-rules", cookies, new
        {
            items = new[] { new { name = "Bad Percent", ruleType = "Percentage", value = 150m, feeLineItemId = (string?)null } }
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceDiscountRules_FlatAmountZero_Returns400()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/discount-rules", cookies, new
        {
            items = new[] { new { name = "Zero Discount", ruleType = "FlatAmount", value = 0m, feeLineItemId = (string?)null } }
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceDiscountRules_EmptyList_Returns200()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        var response = await client.SendAsync(Put($"/api/fee-templates/{id}/discount-rules", cookies, new
        {
            items = Array.Empty<object>()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, body.GetProperty("discountRules").GetArrayLength());
    }

    [Fact]
    public async Task ReplaceDiscountRules_UnknownTemplate_Returns404()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);

        var response = await client.SendAsync(Put($"/api/fee-templates/{Guid.NewGuid()}/discount-rules", cookies, new
        {
            items = Array.Empty<object>()
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── ON DELETE SET NULL behavior ──────────────────────────────────────────

    [Fact]
    public async Task RemoveLineItem_ReferencedByDiscountRule_RuleStaysWithNullLineItemId()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var cookies = await LoginAsAdminAsync(client);
        var (yearId, g5Id, _) = await SeedAsync(client, cookies);

        var template = await CreateTemplateAsync(client, cookies, yearId, g5Id);
        var id = template.GetProperty("id").GetString();

        // Add line item
        var lineResp = await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[]
            {
                new { name = "Tuition Fee", amount = 35000m, displayOrder = 1 },
                new { name = "Lab Fee", amount = 3000m, displayOrder = 2 },
            }
        }));
        var lineBody = await lineResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var tuitionId = lineBody.GetProperty("lineItems")[0].GetProperty("id").GetString();
        var labId = lineBody.GetProperty("lineItems")[1].GetProperty("id").GetString();

        // Add discount rule targeting Lab Fee
        await client.SendAsync(Put($"/api/fee-templates/{id}/discount-rules", cookies, new
        {
            items = new[]
            {
                new { name = "Lab Waiver", ruleType = "Percentage", value = 100m, feeLineItemId = labId }
            }
        }));

        // Remove Lab Fee (send only Tuition Fee)
        await client.SendAsync(Put($"/api/fee-templates/{id}/line-items", cookies, new
        {
            items = new[]
            {
                new { id = tuitionId, name = "Tuition Fee", amount = 35000m, displayOrder = 1 }
            }
        }));

        // Reload and verify the discount rule still exists but has null FeeLineItemId
        var detailResp = await client.SendAsync(Get($"/api/fee-templates/{id}", cookies));
        var detail = await detailResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var rules = detail.GetProperty("discountRules").EnumerateArray().ToList();

        Assert.Single(rules);
        Assert.Equal("Lab Waiver", rules[0].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, rules[0].GetProperty("feeLineItemId").ValueKind);
        Assert.Equal(JsonValueKind.Null, rules[0].GetProperty("feeLineItemName").ValueKind);
    }

    // ─── Auth gates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/fee-templates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeacherRole_Returns403()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        const string teacherEmail = "teacher-fee@demoschool.test";
        const string teacherPassword = "TeacherFee1!";
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
                    DisplayName = "Fee Teacher",
                    Role = UserRole.Teacher,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = teacherEmail, password = teacherPassword });
        var teacherCookies = CookieTestHelpers.BuildCookieHeader(loginResponse);

        var response = await client.SendAsync(Get("/api/fee-templates", teacherCookies));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
