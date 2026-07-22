using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolMgmt.Application.Payments;
using SchoolMgmt.IntegrationTests.Fakes;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.ParentPortal;

// Pay-fees-online (spec 21). Exercises the real controllers/services against Postgres with a FAKE
// IPaymentGateway (no real Stripe). Proves: initiate→confirm marks Paid; the webhook is authoritative
// and idempotent; bad signatures 400; the cross-child IDOR 404s; and Cancel is blocked once paid.
[Collection(IntegrationTestCollection.Name)]
public class ParentPaymentTests(PostgresContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@demoschool.test", password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return CookieTestHelpers.BuildCookieHeader(res);
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

    // Registers the fake gateway (returned so tests can read the created intent id) in the host.
    private (System.Net.Http.HttpClient client, FakeStripePaymentGateway gateway, IDisposable factory) NewHost()
    {
        var gateway = new FakeStripePaymentGateway();
        var factory = fixture.CreateFactory(services =>
            services.AddSingleton<IPaymentGateway>(gateway)); // added last ⇒ wins over the real Stripe one
        return (factory.CreateClient(), gateway, factory);
    }

    // Seeds a child with an ISSUED invoice (1000 total; 600 due in the past/overdue + 400 future),
    // linked to a parent. Returns ids + parent cookies + the invoice id.
    private static async Task<(string child, string year, string parent, string invoiceId)>
        SeedChildWithIssuedInvoiceAsync(HttpClient client, string admin, string tag)
    {
        var gradeId = (await ReadJson(await client.SendAsync(Post("/api/grades", admin,
            new { name = "Grade-" + tag, displayOrder = 60 })))).GetProperty("id").GetString()!;
        var sectionId = (await ReadJson(await client.SendAsync(Post($"/api/grades/{gradeId}/sections", admin,
            new { name = "A" })))).GetProperty("id").GetString()!;

        var createYear = await ReadJson(await client.SendAsync(Post("/api/academic-years", admin,
            new { name = "PAY-" + tag, startDate = "2025-09-01", endDate = "2026-06-30" })));
        var yearId = createYear.GetProperty("id").GetString()!;
        await client.SendAsync(Post($"/api/academic-years/{yearId}/set-current", admin, new { }));

        var email = $"parent-{tag}@demoschool.test";
        var createStudent = await ReadJson(await client.SendAsync(Post("/api/students", admin, new
        {
            firstName = "Pay",
            lastName = "Kid-" + tag,
            dateOfBirth = "2010-03-15",
            gender = "Male",
            enrollmentDate = "2025-09-01",
            guardianName = "Guardian",
            guardianEmail = email,
        })));
        var child = createStudent.GetProperty("id").GetString()!;
        await client.SendAsync(Post($"/api/sections/{sectionId}/enrollments", admin,
            new { studentId = child, academicYearId = yearId }));

        var templateId = (await ReadJson(await client.SendAsync(Post("/api/fee-templates", admin,
            new { name = "Standard-" + tag, academicYearId = yearId, gradeId })))).GetProperty("id").GetString()!;
        await client.SendAsync(Put($"/api/fee-templates/{templateId}/line-items", admin, new
        {
            items = new[] { new { name = "Tuition", amount = 1000m, displayOrder = 1 } }
        }));
        var instBody = await ReadJson(await client.SendAsync(Put($"/api/fee-templates/{templateId}/installments", admin, new
        {
            items = new[]
            {
                new { name = "1st", percentage = 60m, displayOrder = 1 },
                new { name = "2nd", percentage = 40m, displayOrder = 2 },
            }
        })));
        var tInst = instBody.GetProperty("installments").EnumerateArray().ToList();

        await client.SendAsync(Post("/api/fee-assignments/broadcast", admin, new { templateId }));
        await client.SendAsync(Post("/api/fee-invoices/generate", admin, new
        {
            gradeId,
            academicYearId = yearId,
            installmentDueDates = new[]
            {
                new { templateInstallmentId = tInst[0].GetProperty("id").GetString(), dueDate = "2020-01-01" },
                new { templateInstallmentId = tInst[1].GetProperty("id").GetString(), dueDate = "2099-01-01" },
            }
        }));

        var list = await ReadJson(await client.SendAsync(Get(
            $"/api/fee-invoices?studentId={child}&academicYearId={yearId}", admin)));
        var invoiceId = list.GetProperty("items")[0].GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Post($"/api/fee-invoices/{invoiceId}/issue", admin, new { }))).StatusCode);

        // Create the parent login and log in.
        var pl = await ReadJson(await client.SendAsync(Post($"/api/students/{child}/parent-login", admin,
            new { temporaryPassword = "Passw0rd!" })));
        _ = pl.GetProperty("parentUserId").GetString();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd!" });
        var parent = CookieTestHelpers.BuildCookieHeader(login);

        return (child, yearId, parent, invoiceId);
    }

    // First (overdue, 600) installment id for a child, read from the parent fee overview.
    private static async Task<(string firstInstId, decimal firstAmount)> FirstInstallmentAsync(
        HttpClient client, string child, string parent)
    {
        var fees = await ReadJson(await client.SendAsync(Get($"/api/parent/children/{child}/fees", parent)));
        var installments = fees.GetProperty("invoice").GetProperty("installments").EnumerateArray()
            .OrderBy(i => i.GetProperty("displayOrder").GetInt32()).ToList();
        return (installments[0].GetProperty("id").GetString()!, installments[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task PayThenConfirm_MarksInstallmentPaid_UpdatesBalance()
    {
        var (client, gateway, factory) = NewHost();
        using var _ = factory;
        var admin = await LoginAsAdminAsync(client);
        var (child, _, parent, _) = await SeedChildWithIssuedInvoiceAsync(client, admin, Uniq());
        var (instId, _) = await FirstInstallmentAsync(client, child, parent);

        var pay = await client.SendAsync(Post(
            $"/api/parent/children/{child}/installments/{instId}/pay", parent, new { }));
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        var payBody = await ReadJson(pay);
        var paymentId = payBody.GetProperty("paymentId").GetString()!;
        Assert.False(string.IsNullOrEmpty(payBody.GetProperty("clientSecret").GetString()));
        Assert.Equal(60000, gateway.LastAmountMinorUnits); // 600.00 * 100

        gateway.ReturnPathSucceeds = true;
        var confirm = await client.SendAsync(Post(
            $"/api/parent/children/{child}/payments/{paymentId}/confirm", parent, new { }));
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var fees = await ReadJson(await client.SendAsync(Get($"/api/parent/children/{child}/fees", parent)));
        var summary = fees.GetProperty("summary");
        Assert.Equal(600m, summary.GetProperty("totalPaid").GetDecimal());
        Assert.Equal(400m, summary.GetProperty("outstanding").GetDecimal());
        Assert.Equal(0, summary.GetProperty("overdueCount").GetInt32()); // the overdue one is now paid
        var paidInst = fees.GetProperty("invoice").GetProperty("installments").EnumerateArray()
            .First(i => i.GetProperty("id").GetString() == instId);
        Assert.Equal("Paid", paidInst.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Webhook_MarksPaid_AndIsIdempotent()
    {
        var (client, gateway, factory) = NewHost();
        using var _ = factory;
        var admin = await LoginAsAdminAsync(client);
        var (child, _, parent, _) = await SeedChildWithIssuedInvoiceAsync(client, admin, Uniq());
        var (instId, _) = await FirstInstallmentAsync(client, child, parent);

        await client.SendAsync(Post($"/api/parent/children/{child}/installments/{instId}/pay", parent, new { }));
        var intentId = gateway.LastCreatedIntentId;

        async Task<HttpResponseMessage> PostWebhook(string signature)
        {
            var msg = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { paymentIntentId = intentId, succeeded = true }),
                    Encoding.UTF8, "application/json"),
            };
            msg.Headers.Add("Stripe-Signature", signature);
            return await client.SendAsync(msg);
        }

        // First and second delivery of the same succeeded event — both 200, applied exactly once.
        Assert.Equal(HttpStatusCode.OK, (await PostWebhook(FakeStripePaymentGateway.ValidSignature)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhook(FakeStripePaymentGateway.ValidSignature)).StatusCode);

        var fees = await ReadJson(await client.SendAsync(Get($"/api/parent/children/{child}/fees", parent)));
        Assert.Equal(600m, fees.GetProperty("summary").GetProperty("totalPaid").GetDecimal());
        var paidInst = fees.GetProperty("invoice").GetProperty("installments").EnumerateArray()
            .First(i => i.GetProperty("id").GetString() == instId);
        Assert.Equal("Paid", paidInst.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Webhook_BadSignature_Returns400_NoStateChange()
    {
        var (client, gateway, factory) = NewHost();
        using var _ = factory;
        var admin = await LoginAsAdminAsync(client);
        var (child, _, parent, _) = await SeedChildWithIssuedInvoiceAsync(client, admin, Uniq());
        var (instId, _) = await FirstInstallmentAsync(client, child, parent);
        await client.SendAsync(Post($"/api/parent/children/{child}/installments/{instId}/pay", parent, new { }));

        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { paymentIntentId = gateway.LastCreatedIntentId, succeeded = true }),
                Encoding.UTF8, "application/json"),
        };
        msg.Headers.Add("Stripe-Signature", "forged");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(msg)).StatusCode);

        var fees = await ReadJson(await client.SendAsync(Get($"/api/parent/children/{child}/fees", parent)));
        Assert.Equal(0m, fees.GetProperty("summary").GetProperty("totalPaid").GetDecimal());
    }

    [Fact]
    public async Task Webhook_IsAnonymous_NoAuthRequired()
    {
        // A webhook carries no cookie/JWT. Even with a valid signature but an unknown intent,
        // it must be accepted (200) — proving the endpoint is reachable without auth.
        var (client, _, factory) = NewHost();
        using var _ = factory;

        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { paymentIntentId = "pi_unknown", succeeded = true }),
                Encoding.UTF8, "application/json"),
        };
        msg.Headers.Add("Stripe-Signature", FakeStripePaymentGateway.ValidSignature);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(msg)).StatusCode);
    }

    [Fact]
    public async Task Pay_InstallmentOfAnotherChild_Returns404()
    {
        var (client, _, factory) = NewHost();
        using var _ = factory;
        var admin = await LoginAsAdminAsync(client);

        var (childA, _, parentA, _) = await SeedChildWithIssuedInvoiceAsync(client, admin, Uniq());
        var (childB, _, parentB, _) = await SeedChildWithIssuedInvoiceAsync(client, admin, Uniq());
        var (instB, _) = await FirstInstallmentAsync(client, childB, parentB);

        // Parent A, routing through their OWN linked child, tries to pay child B's installment id.
        var res = await client.SendAsync(Post(
            $"/api/parent/children/{childA}/installments/{instB}/pay", parentA, new { }));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Pay_ChildOfAnotherParent_Returns404()
    {
        var (client, _, factory) = NewHost();
        using var _ = factory;
        var admin = await LoginAsAdminAsync(client);

        var (_, _, parentA, _) = await SeedChildWithIssuedInvoiceAsync(client, admin, Uniq());
        var (childB, _, parentB, _) = await SeedChildWithIssuedInvoiceAsync(client, admin, Uniq());
        var (instB, _) = await FirstInstallmentAsync(client, childB, parentB);

        var res = await client.SendAsync(Post(
            $"/api/parent/children/{childB}/installments/{instB}/pay", parentA, new { }));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Pay_Unauthenticated_Returns401()
    {
        var (client, _, factory) = NewHost();
        using var _ = factory;
        var res = await client.PostAsync(
            $"/api/parent/children/{Guid.NewGuid()}/installments/{Guid.NewGuid()}/pay", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Pay_AdminRole_Returns403()
    {
        var (client, _, factory) = NewHost();
        using var _ = factory;
        var admin = await LoginAsAdminAsync(client);
        var res = await client.SendAsync(Post(
            $"/api/parent/children/{Guid.NewGuid()}/installments/{Guid.NewGuid()}/pay", admin, new { }));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CancelInvoice_AfterPayment_IsBlocked()
    {
        var (client, gateway, factory) = NewHost();
        using var _ = factory;
        var admin = await LoginAsAdminAsync(client);
        var (child, _, parent, invoiceId) = await SeedChildWithIssuedInvoiceAsync(client, admin, Uniq());
        var (instId, _) = await FirstInstallmentAsync(client, child, parent);

        var pay = await ReadJson(await client.SendAsync(Post(
            $"/api/parent/children/{child}/installments/{instId}/pay", parent, new { })));
        gateway.ReturnPathSucceeds = true;
        await client.SendAsync(Post(
            $"/api/parent/children/{child}/payments/{pay.GetProperty("paymentId").GetString()}/confirm", parent, new { }));

        var cancel = await client.SendAsync(Post($"/api/fee-invoices/{invoiceId}/cancel", admin, new { }));
        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
    }
}
