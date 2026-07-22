# Spec 21 — Implement Parent Portal: Pay Fees Online (Backend + Frontend)

## Related docs & prior specs

- **Idea doc**: [docs/ideas/18-parent-portal-pay-fees-online.md](../docs/ideas/18-parent-portal-pay-fees-online.md) — problem statement and the six resolved decisions (one installment per transaction · Stripe test mode, direct with a thin seam · webhook-authoritative + reconcile-on-return, one idempotent reconcile · dedicated `Payment` entity · publicly-reachable deploy for webhook delivery · Stripe Elements inline). Also: why the webhook lives outside the auth mechanism, the tenant-from-metadata bypass, the not-doing list.
- **Parent Portal — view fees** ([specs/20-implement-parent-portal-fees.md](20-implement-parent-portal-fees.md) / [docs/ideas/17-parent-portal-fees.md](../docs/ideas/17-parent-portal-fees.md)) — the direct predecessor. Provides `ChildFeesPage` (balance hero + installment schedule + line-item breakdown), `FeeInvoiceService.GetStudentFeeOverviewAsync` (the server-owned money rollup that reads `AmountPaid`), `StudentFeeOverviewDto`/`StudentFeeSummaryDto`, and the `parentPortalApi.getChildFees` client. This slice **makes `TotalPaid`/`Outstanding` move** by writing `AmountPaid`/`PaidAt`/`Status=Paid` onto installments — the rollup and UI already read those, so balance + admin dashboard update with **zero rework**.
- **Parent Portal — grades / attendance** ([specs/18](18-implement-parent-portal-grades.md), [specs/19](19-implement-parent-portal-attendance.md)) — established the `api/parent` controller, `ParentPortalService`, the single `ResolveLinkedChildOrThrow(parentUserId, childId)` guard (unlinked/unknown child → **404**), JWT-`sub` caller identity, and the parent shell. This slice adds the **first parent write path** to that controller (POST, not GET) plus a **new, separate, anonymous webhook controller**.
- **Fee invoicing** ([specs/12-implement-fee-invoicing.md](12-implement-fee-invoicing.md) / [docs/ideas/08-fee-invoicing.md](../docs/ideas/08-fee-invoicing.md)) — provides `FeeInvoice`, `FeeInvoiceInstallment` (already carrying `Status`/`AmountPaid`/`PaidAt`, payment-ready), `InstallmentStatus (Pending/Paid/Overdue)`, `InvoiceStatus (Draft/Issued/Cancelled)`, `FeeInvoiceService`, `IFeeInvoiceRepository`, and `IOptions<InvoiceOptions>` (the options pattern this spec mirrors for `StripeOptions`). This spec adds the Cancel guard: **an invoice can no longer be Cancelled once any `Payment` on it has Succeeded.**
- **Multi-tenancy** ([specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md)) — `ITenantScoped`, `BaseEntity`, the EF Core global query filter keyed on `ITenantProvider.CurrentSchoolId`. **Critical for the webhook:** `HttpContextTenantProvider.CurrentSchoolId` *throws* with no JWT claims, and the query filter calls it — so the webhook path must not hit the filter (see A9).
- **Auth** ([specs/02-implement-auth.md](02-implement-auth.md)) — JWT-in-`SameSite=Lax`-cookie, `[Authorize(Roles = "Parent")]`, `User.FindFirstValue("sub")`/`"school_id"`. The webhook is the sole `[AllowAnonymous]` endpoint, authenticated by Stripe signature instead.
- **Rules**: [.claude/rules/backend.md](../.claude/rules/backend.md) — thin controllers, one service per feature area, repositories touch only the `DbSet` (no `SaveChanges`/transactions), `IUnitOfWork` owns persistence, **GET must be side-effect-free** (all mutations here are POST), DI conventions, interfaces-where-consumed, `Scoped` default.

## Overview

A logged-in **Parent** views a child's Issued invoice (spec 20) and, for any not-fully-paid installment, clicks **Pay**. A Stripe Elements card form (test card `4242 4242 4242 4242`) opens inline in a modal — no redirect. On success the installment flips **Pending → Paid**, a `Payment` row records the transaction, the balance hero and dashboard reflect it, and a red "Overdue" badge (if any) clears.

The design centres on one invariant: **a single idempotent `ReconcilePayment` is the only code that marks an installment Paid / a `Payment` Succeeded.** Two callers invoke it — the **Stripe webhook** (authoritative, anonymous, signature-verified) and the **authenticated return path** (for instant UI) — and it is a no-op if the `Payment` is already Succeeded. Double webhooks, double-clicks, and the webhook/return race are therefore all safe by construction.

Three flows:
1. **Initiate** (parent, authenticated POST) — re-derive the amount server-side, create a `Payment(Pending)` + a Stripe `PaymentIntent` stamping `SchoolId`/`PaymentId`/`InstallmentId` into `metadata`, return the client secret.
2. **Confirm on return** (parent, authenticated POST) — after `confirmCardPayment` resolves, verify the intent with Stripe and call `ReconcilePayment`.
3. **Webhook** (Stripe → anonymous POST) — verify signature, extract intent, call `ReconcilePayment`. Authoritative.

**Scope (from idea doc 18):**
- **In:** `Payment` entity (+ migration); `StripeOptions`; `IPaymentGateway` thin seam + `StripePaymentGateway`; `IPaymentRepository`; `PaymentService` (`InitiateInstallmentPaymentAsync`, `ReconcilePaymentAsync`); two parent POST endpoints; one anonymous webhook controller; Cancel-guard in `FeeInvoiceService`; frontend Pay button + Stripe Elements modal on `ChildFeesPage`.
- **Out:** partial payments; refunds (Cancel is *blocked* once paid instead); saved cards/wallets/GCash; multi-currency; receipt PDF/email; admin manual/cash recording; persisted overdue/cron.

---

## Part A — Backend

### A1. NuGet

Add **`Stripe.net`** (latest stable) to `SchoolMgmt.Infrastructure` only (the concrete gateway lives there; Application depends on the `IPaymentGateway` abstraction, never on `Stripe.net`).

### A2. `Payment` entity (Domain)

```csharp
// SchoolMgmt.Domain/Enums/PaymentStatus.cs
public enum PaymentStatus { Pending, Succeeded, Failed }

// SchoolMgmt.Domain/Entities/Payment.cs
public class Payment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeInvoiceInstallmentId { get; set; }
    public FeeInvoiceInstallment FeeInvoiceInstallment { get; set; } = null!;
    public decimal Amount { get; set; }                 // snapshot of remaining amount at initiation
    public string Currency { get; set; } = "PHP";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string StripePaymentIntentId { get; set; } = string.Empty; // unique; the idempotency key
    public string? FailureReason { get; set; }
    public DateTime? PaidAt { get; set; }
}
```

- One row per intent (the **collections audit trail**). `Failed` rows are kept (idea doc: "leaning keep").
- `FeeInvoiceInstallment` back-nav is optional; keep the FK id. No collection is added to `FeeInvoiceInstallment` (avoid touching the shared entity/DTO; parent + admin fee reads are unchanged).

### A3. EF configuration + migration

```csharp
// SchoolMgmt.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.StripePaymentIntentId).IsRequired().HasMaxLength(255);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>();     // match existing enum-as-string convention
        builder.HasIndex(x => x.StripePaymentIntentId).IsUnique();   // idempotency backstop at the DB
        builder.HasOne(x => x.FeeInvoiceInstallment)
            .WithMany()
            .HasForeignKey(x => x.FeeInvoiceInstallmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- Add `public DbSet<Payment> Payments => Set<Payment>();` to `AppDbContext`. Configs are auto-applied via `ApplyConfigurationsFromAssembly`; the tenant filter is auto-attached because `Payment : ITenantScoped`.
- Migration: `dotnet ef migrations add AddPayment -p SchoolMgmt.Infrastructure -s SchoolMgmt.WebApi`. One new table, no changes to existing tables.

### A4. `StripeOptions` (Application) + config

```csharp
// SchoolMgmt.Application/Payments/StripeOptions.cs
public class StripeOptions
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;       // sk_test_...
    public string PublishableKey { get; set; } = string.Empty;  // pk_test_... (served to the client, see A11)
    public string WebhookSigningSecret { get; set; } = string.Empty; // whsec_...
    public string Currency { get; set; } = "PHP";
}
```

- Bind in `Infrastructure/DependencyInjection.cs`: `services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));`
- `appsettings.json`: a `"Stripe"` section with **empty** secret/webhook values (never commit test keys); real values via user-secrets / env vars (`Stripe__SecretKey`, etc.). Document in the section's README/appsettings comment.

### A5. `IPaymentGateway` seam (Application) + `StripePaymentGateway` (Infrastructure)

A **thin** abstraction — just enough to keep `Stripe.net` out of Application and to make `PaymentService` unit-testable with a fake. Not a multi-provider framework.

```csharp
// SchoolMgmt.Application/Payments/IPaymentGateway.cs
public record CreatedIntent(string PaymentIntentId, string ClientSecret);
public record VerifiedIntent(string PaymentIntentId, bool Succeeded, string? FailureReason);

public interface IPaymentGateway
{
    // amountMinorUnits = pesos * 100 (integer). metadata carries SchoolId/PaymentId/InstallmentId.
    Task<CreatedIntent> CreateIntentAsync(
        long amountMinorUnits, string currency, IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    // Retrieve + report status. Used by the return path to avoid trusting the client's success claim.
    Task<VerifiedIntent> GetIntentAsync(string paymentIntentId, CancellationToken ct = default);

    // Verify the Stripe-Signature header against the raw body; parse the event; if it is a
    // payment_intent.succeeded / .payment_failed, return the intent, else null.
    VerifiedIntent? ParseWebhookEvent(string rawJson, string stripeSignatureHeader);
}
```

`StripePaymentGateway` (Infrastructure) implements these with `PaymentIntentService`, `EventUtility.ConstructEvent(rawJson, sig, webhookSecret)`, reading keys from `IOptions<StripeOptions>`. Register `AddScoped<IPaymentGateway, StripePaymentGateway>()`.

**Minor-units conversion lives in one helper** (idea-doc open question resolved): a private `static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);` in `PaymentService` (PHP has 2 minor digits). Do not scatter `* 100` elsewhere.

### A6. `IPaymentRepository` (Application) + impl (Infrastructure)

The **only sanctioned bypass** of the tenant query filter lives here, in explicitly-named, commented methods used solely by the webhook/reconcile path (which has no JWT claims).

```csharp
// SchoolMgmt.Application/Payments/IPaymentRepository.cs
public interface IPaymentRepository : IRepository<Payment>
{
    // Normal, tenant-scoped: used by the authenticated initiate path.
    Task<Payment?> GetByIntentIdAsync(string paymentIntentId, CancellationToken ct = default);

    // TENANT-FILTER BYPASS — webhook/reconcile only (no JWT ⇒ no claims ⇒ the global filter
    // would throw). Looks the Payment up by its globally-unique Stripe intent id, then the caller
    // MUST assert the returned Payment.SchoolId equals the SchoolId in the verified intent metadata.
    Task<Payment?> GetByIntentIdIgnoringTenantAsync(string paymentIntentId, CancellationToken ct = default);
}
```

Impl notes (Infrastructure `PaymentRepository`):
- `GetByIntentIdAsync` → `DbSet.FirstOrDefaultAsync(p => p.StripePaymentIntentId == id, ct)` (filter applies).
- `GetByIntentIdIgnoringTenantAsync` → `DbSet.IgnoreQueryFilters().Include(p => p.FeeInvoiceInstallment).ThenInclude(i => i.FeeInvoice).ThenInclude(inv => inv.Installments).FirstOrDefaultAsync(...)`. Include the installment + its invoice + sibling installments so reconcile can mutate and (optionally) recompute state without another claim-dependent query.

Register `AddScoped<IPaymentRepository, PaymentRepository>()`.

### A7. `PaymentService.InitiateInstallmentPaymentAsync`

Authenticated path. `PaymentService` injects `IFeeInvoiceRepository`, `IPaymentRepository`, `IPaymentGateway`, `IUnitOfWork`, `ITenantProvider`, `IDateTimeProvider`, `IOptions<StripeOptions>`.

```csharp
public async Task<InitiatePaymentResult> InitiateInstallmentPaymentAsync(
    Guid installmentId, CancellationToken ct = default)
{
    // Load the installment with its invoice (tenant-scoped — this path is authenticated).
    var installment = await feeInvoiceRepo.GetInstallmentWithInvoiceAsync(installmentId, ct)
        ?? throw new NotFoundException("Installment not found.");

    if (installment.FeeInvoice.Status != InvoiceStatus.Issued)
        throw new DomainException("Only issued invoices can be paid.");

    var remaining = installment.Amount - (installment.AmountPaid ?? 0m);
    if (remaining <= 0m)
        throw new DomainException("This installment is already paid.");   // server re-derives; ignores client amount

    var schoolId = tenantProvider.CurrentSchoolId;
    var payment = new Payment
    {
        FeeInvoiceInstallmentId = installment.Id,
        Amount = remaining,
        Currency = options.Value.Currency,
        Status = PaymentStatus.Pending,
        StripePaymentIntentId = "",           // set below, once the intent exists
    };
    await paymentRepo.AddAsync(payment, ct);
    await unitOfWork.SaveChangesAsync(ct);    // persists Payment.Id + stamps SchoolId (Added path)

    var intent = await gateway.CreateIntentAsync(
        ToMinorUnits(remaining), payment.Currency,
        new Dictionary<string, string>
        {
            ["schoolId"] = schoolId.ToString(),
            ["paymentId"] = payment.Id.ToString(),
            ["installmentId"] = installment.Id.ToString(),
        }, ct);

    payment.StripePaymentIntentId = intent.PaymentIntentId;
    paymentRepo.Update(payment);
    await unitOfWork.SaveChangesAsync(ct);

    return new InitiatePaymentResult(payment.Id, intent.ClientSecret, options.Value.PublishableKey);
}
```

- Needs one `IFeeInvoiceRepository` addition: `GetInstallmentWithInvoiceAsync(Guid installmentId)` → the installment with `.FeeInvoice` (and its `Installments` if the Cancel guard/rollup wants them). Tenant-scoped.
- `InitiatePaymentResult(Guid PaymentId, string ClientSecret, string PublishableKey)`.
- **Concurrency (idea-doc open question):** the unique index on `StripePaymentIntentId` + the `remaining <= 0` guard are the backstop; a second guardian initiating while one intent is pending creates a second intent, but reconcile is idempotent per intent and the `remaining` recomputation on the *second* reconcile will already see 0 and no-op. Acceptable for the demo; noted, not solved with a lock.

### A8. `PaymentService.ReconcilePaymentAsync` — the single writer (idempotent)

Called by **both** the return path and the webhook. `expectedSchoolId` is the tenant claim on the return path, and the verified-metadata `schoolId` on the webhook path — the method never reads ambient claims, so it behaves identically in both contexts.

```csharp
// The ONLY code that marks an installment Paid / a Payment Succeeded. Idempotent: a no-op if the
// Payment is already terminal. Safe under double webhooks, double-clicks, and webhook/return races.
public async Task ReconcilePaymentAsync(
    VerifiedIntent verified, Guid expectedSchoolId, CancellationToken ct = default)
{
    // BYPASS the tenant filter (webhook has no claims); re-scope manually below.
    var payment = await paymentRepo.GetByIntentIdIgnoringTenantAsync(verified.PaymentIntentId, ct);
    if (payment is null) return;                          // unknown intent — ignore (nothing to reconcile)

    if (payment.SchoolId != expectedSchoolId)             // defense in depth: metadata/claim must match the row
        throw new DomainException("Payment tenant mismatch.");

    if (payment.Status != PaymentStatus.Pending) return;  // IDEMPOTENT no-op — already Succeeded/Failed

    if (!verified.Succeeded)
    {
        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = verified.FailureReason;
        paymentRepo.Update(payment);
        await unitOfWork.SaveChangesAsync(ct);            // Modified-only ⇒ no tenant-provider access
        return;
    }

    var installment = payment.FeeInvoiceInstallment;      // eager-loaded by the ignoring query
    payment.Status = PaymentStatus.Succeeded;
    payment.PaidAt = dateTimeProvider.UtcNow.UtcDateTime;

    installment.AmountPaid = payment.Amount;              // full remaining amount (one installment, full pay)
    installment.PaidAt = payment.PaidAt;
    installment.Status = InstallmentStatus.Paid;

    paymentRepo.Update(payment);
    // installment is tracked via the include; mark modified through its repo if required by UoW conventions.
    await unitOfWork.SaveChangesAsync(ct);                // Modified-only ⇒ safe without claims
}
```

Key points:
- **Idempotency** is the `Status != Pending → return` gate, backed by the unique index.
- **No tenant-provider access on write:** only *Modified* entities → `AppDbContext.SaveChangesAsync` never touches `CurrentSchoolId`. This is what lets the webhook (no claims) save.
- `AmountPaid = payment.Amount` (the remaining snapshot) — consistent with "one installment, full amount." `TotalPaid`/`Outstanding` in `GetStudentFeeOverviewAsync` (spec 20) already sum `AmountPaid`, so the balance updates automatically.
- `InstallmentStatus.Overdue` is still never written — overdue stays derived at read time (spec 20 invariant holds).

### A9. `ParentPortalService` — two authenticated methods (guarded)

```csharp
// Inject PaymentService into ParentPortalService (alongside gradebook/attendance/fees).

// POST — parent starts paying one installment of a linked child's invoice.
public async Task<InitiatePaymentResult> PayChildInstallmentAsync(
    Guid parentUserId, Guid childId, Guid installmentId, CancellationToken ct = default)
{
    await ResolveLinkedChildOrThrow(parentUserId, childId, ct);          // AUTHORIZATION
    return await payments.InitiateInstallmentPaymentAsync(installmentId, ct);
    // Note: installment→child ownership is enforced because the installment is loaded tenant-scoped
    // AND belongs to an invoice for this child; see A10 for the cross-child guard.
}

// POST — parent confirms after Stripe.js resolves; verify with Stripe, then reconcile.
public async Task ConfirmChildPaymentAsync(
    Guid parentUserId, Guid childId, Guid paymentId, CancellationToken ct = default)
{
    await ResolveLinkedChildOrThrow(parentUserId, childId, ct);          // AUTHORIZATION
    await payments.ConfirmPaymentAsync(paymentId, tenantProvider.CurrentSchoolId, ct);
}
```

`PaymentService.ConfirmPaymentAsync(paymentId, schoolId, ct)`: load the `Payment` (tenant-scoped `GetByIntentId`/`GetById`), `gateway.GetIntentAsync(intentId)`, then `ReconcilePaymentAsync(verified, schoolId, ct)`. The return path **never trusts a client "it succeeded"** — it re-checks with Stripe.

**Cross-child guard (important):** `installmentId`/`paymentId` are client-supplied. The link guard proves the *child* belongs to the caller, but not that the *installment* belongs to that child. Add a check: after loading the installment/payment, assert `installment.FeeInvoice.StudentId == childId` (else `NotFoundException`). This closes the IDOR where a linked parent pays an installment belonging to some other student via a guessed id.

### A10. `ParentPortalController` — two POST routes

```csharp
// POST /api/parent/children/{childId}/installments/{installmentId}/pay
[HttpPost("children/{childId:guid}/installments/{installmentId:guid}/pay")]
public async Task<IActionResult> PayInstallment(
    Guid childId, Guid installmentId, CancellationToken ct)
    => Ok(await service.PayChildInstallmentAsync(ParentUserId, childId, installmentId, ct));

// POST /api/parent/children/{childId}/payments/{paymentId}/confirm
[HttpPost("children/{childId:guid}/payments/{paymentId:guid}/confirm")]
public async Task<IActionResult> ConfirmPayment(
    Guid childId, Guid paymentId, CancellationToken ct)
{
    await service.ConfirmChildPaymentAsync(ParentUserId, childId, paymentId, ct);
    return NoContent();
}
```

These are **POST** (mutations) — consistent with the SameSite=Lax CSRF rule; they are same-origin XHR carrying the cookie normally, Parent-only via the controller-level `[Authorize(Roles = "Parent")]`.

| Method | Route | Success | Errors |
|---|---|---|---|
| POST | `/api/parent/children/{childId}/installments/{installmentId}/pay` | 200 `InitiatePaymentResult` | 404 child not linked / installment not for child; 400 invoice not Issued or already paid |
| POST | `/api/parent/children/{childId}/payments/{paymentId}/confirm` | 204 | 404 child not linked / payment not for child |

### A11. Publishable key to the client

The Elements form needs the `pk_test_...` publishable key. Return it in `InitiatePaymentResult` (as above) — simplest, no extra endpoint, and it is public by definition. (Alternative: a tiny `GET /api/parent/stripe-config`; not needed.)

### A12. Webhook controller (new, anonymous, signature-verified)

Separate controller so it carries **no** `[Authorize]` and is visibly outside the parent auth surface.

```csharp
// SchoolMgmt.WebApi/Controllers/StripeWebhookController.cs
[ApiController]
[Route("api/webhooks/stripe")]
[AllowAnonymous]
public class StripeWebhookController(IPaymentGateway gateway, PaymentService payments) : ControllerBase
{
    // POST /api/webhooks/stripe  — Stripe → us, server-to-server, no cookie/JWT.
    // Auth = Stripe signature (HMAC over the raw body). Tenant = the verified intent metadata.
    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);
        var sig = Request.Headers["Stripe-Signature"].ToString();

        VerifiedIntent? verified;
        try { verified = gateway.ParseWebhookEvent(rawJson, sig); }
        catch { return BadRequest(); }                 // bad/forged signature → 400, no state touched

        if (verified is null) return Ok();             // event we don't handle → 200 so Stripe stops retrying

        var schoolId = /* parse "schoolId" from the verified intent metadata */;
        await payments.ReconcilePaymentAsync(verified, schoolId, ct);
        return Ok();                                   // 200 tells Stripe it was delivered
    }
}
```

- **Raw body**: read the stream directly (as above) — do not model-bind, or the signature can't be verified. `ParseWebhookEvent` must also surface the intent `metadata` (so the controller can read `schoolId`); adjust `VerifiedIntent` to include a `IReadOnlyDictionary<string,string> Metadata` (or return schoolId directly). Prefer: `ParseWebhookEvent` returns the intent id + succeeded + failureReason + metadata; the controller pulls `schoolId` from metadata.
- **Always 200 on handled/ignored**, 400 only on signature failure — standard Stripe practice so Stripe doesn't hammer retries.
- No `DomainExceptionFilter` concern: a tenant mismatch inside reconcile throws `DomainException` → 400 (Stripe will retry; acceptable, and it should never happen with correct metadata).
- **Pipeline:** the endpoint is anonymous; `app.UseAuthentication()/UseAuthorization()` already allow anonymous routes through, so **no `Program.cs` change** is required beyond the controller existing. Confirm no global auth filter forces auth (there isn't one — auth is per-`[Authorize]`).

### A13. Cancel guard in `FeeInvoiceService.CancelInvoiceAsync`

```csharp
// before setting Cancelled:
var hasSucceededPayment = await paymentRepo.AnySucceededForInvoiceAsync(invoice.Id, ct);
if (hasSucceededPayment)
    throw new DomainException("Cannot cancel an invoice that has received payments.");
```

Add `IPaymentRepository.AnySucceededForInvoiceAsync(Guid feeInvoiceId, ct)` (tenant-scoped; joins Payment → installment → invoice, `Status == Succeeded`). Inject `IPaymentRepository` into `FeeInvoiceService`. This is the "no refunds; block cancel instead" decision made coherent.

### A14. DI registration (Infrastructure `DependencyInjection.cs`)

```csharp
services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
services.AddScoped<IPaymentRepository, PaymentRepository>();
services.AddScoped<IPaymentGateway, StripePaymentGateway>();
```

`PaymentService` is an Application service — register it where `FeeInvoiceService`/`ParentPortalService` are registered (Application `DependencyInjection.cs`, `AddScoped<PaymentService>()`), matching the existing concrete-service registration pattern.

### A15. Project structure (backend)

```
backend/
  SchoolMgmt.Domain/
    Enums/PaymentStatus.cs                    # new
    Entities/Payment.cs                       # new
  SchoolMgmt.Application/
    Payments/
      IPaymentGateway.cs                      # new (+ CreatedIntent/VerifiedIntent records)
      IPaymentRepository.cs                   # new
      PaymentService.cs                       # new (Initiate + Confirm + Reconcile)
      StripeOptions.cs                        # new
      Dtos/InitiatePaymentResult.cs           # new
    FeeInvoices/
      IFeeInvoiceRepository.cs                # modified — GetInstallmentWithInvoiceAsync
      FeeInvoiceService.cs                    # modified — Cancel guard (inject IPaymentRepository)
    ParentPortal/ParentPortalService.cs       # modified — inject PaymentService; Pay + Confirm methods
    DependencyInjection.cs                     # modified — AddScoped<PaymentService>()
  SchoolMgmt.Infrastructure/
    Payments/StripePaymentGateway.cs           # new
    Persistence/
      AppDbContext.cs                          # modified — DbSet<Payment>
      Configurations/PaymentConfiguration.cs   # new
      Repositories/
        PaymentRepository.cs                   # new (incl. the one IgnoreQueryFilters method)
        FeeInvoiceRepository.cs                # modified — GetInstallmentWithInvoiceAsync
    Migrations/*_AddPayment.cs                  # new
    DependencyInjection.cs                      # modified — StripeOptions, IPaymentRepository, IPaymentGateway
  SchoolMgmt.WebApi/
    Controllers/
      ParentPortalController.cs                # modified — two POST routes
      StripeWebhookController.cs               # new — anonymous, signature-verified
    appsettings.json                           # modified — empty "Stripe" section
```

---

## Part B — Frontend

### B1. Packages

Add **`@stripe/stripe-js`** and **`@stripe/react-stripe-js`** to `frontend/package.json`.

### B2. API client (`parentPortal.ts`)

```ts
export interface InitiatePaymentResult {
  paymentId: string
  clientSecret: string
  publishableKey: string
}

// add to parentPortalApi:
payInstallment: (childId: string, installmentId: string) =>
  api.post<InitiatePaymentResult>(
    `/parent/children/${childId}/installments/${installmentId}/pay`,
  ).then((r) => r.data),

confirmPayment: (childId: string, paymentId: string) =>
  api.post<void>(`/parent/children/${childId}/payments/${paymentId}/confirm`).then(() => undefined),
```

(No new query key; success just invalidates `PARENT_KEYS.childFees(childId, academicYearId)`.)

### B3. `ChildFeesPage` — Pay action + modal

`ChildFeesPage` and its installment table already exist (spec 20). Changes:
- **Pay button per row**: in the Status cell (or a new trailing Actions cell), render a **Pay** button when the row is not `Paid` (i.e. `inst.status !== 'Paid'`), for any installment with `amount - (amountPaid ?? 0) > 0`. Overdue rows still show the red badge *and* a Pay button.
- Clicking Pay opens a `PayInstallmentModal` with the installment (name + `currencyFmt.format(remaining)`).

### B4. `PayInstallmentModal` (new)

`frontend/src/pages/parent/fees/PayInstallmentModal.tsx`. Flow:
1. On open, call `parentPortalApi.payInstallment(childId, installmentId)` → `{ paymentId, clientSecret, publishableKey }`.
2. `loadStripe(publishableKey)` (memoize per publishable key), wrap the form in `<Elements stripe={stripePromise} options={{ clientSecret }}>`.
3. Render `<PaymentElement />` (or `<CardElement />`) + a Pay button.
4. On submit: `stripe.confirmPayment({ elements, redirect: 'if_required' })` (inline, no redirect). On `paymentIntent.status === 'succeeded'`:
   - call `parentPortalApi.confirmPayment(childId, paymentId)` (return-path reconcile, instant UI),
   - `queryClient.invalidateQueries(PARENT_KEYS.childFees(childId, academicYearId))`,
   - close the modal, toast success.
5. On card error / failed intent: show the Stripe error message inline; leave the modal open to retry (a new intent is created on reopen).
6. Loading/disabled states while the intent is being created and while confirming.

Use the existing modal/dialog + button primitives already used elsewhere (match `CreateParentLoginModal` / other modals for styling).

### B5. Project structure (frontend)

```
frontend/src/
  api/parentPortal.ts                          # modified — InitiatePaymentResult, payInstallment, confirmPayment
  pages/parent/fees/
    ChildFeesPage.tsx                          # modified — Pay button per unpaid installment
    PayInstallmentModal.tsx                    # new — Stripe Elements inline flow
  package.json                                 # modified — @stripe/stripe-js, @stripe/react-stripe-js
```

No route/nav change — "Fees" already exists (spec 20).

---

## Implementation order

1. **Domain + persistence**: `PaymentStatus`, `Payment`, `PaymentConfiguration`, `DbSet`, migration. Build.
2. **Gateway + options + repo**: `StripeOptions`, `IPaymentGateway` + `StripePaymentGateway`, `IPaymentRepository` + impl (incl. the commented `IgnoreQueryFilters` method), DI. Build.
3. **Service**: `PaymentService` (`Initiate`, `Confirm`, `Reconcile`), `IFeeInvoiceRepository.GetInstallmentWithInvoiceAsync`, Cancel guard. **Unit tests for reconcile idempotency + tenant mismatch first.**
4. **Endpoints**: `ParentPortalService` methods + controller POST routes; `StripeWebhookController`. Integration tests. `dotnet test SchoolMgmt.slnx`.
5. **Frontend**: packages → `parentPortal.ts` → `PayInstallmentModal` → `ChildFeesPage` Pay button. `npm run build`.
6. Manual/demo: `stripe` test-mode dashboard webhook → deployed URL; pay with `4242…`; confirm installment flips Paid, balance + dashboard update.

Commit after backend-complete (with tests green) and after the frontend flow.

---

## Key invariants

- **One writer of payment truth** — only `ReconcilePaymentAsync` marks an installment Paid / a Payment Succeeded; it is idempotent (no-op once terminal) and backed by a unique index on `StripePaymentIntentId`.
- **Webhook is authoritative; return path is UX** — both call the same reconcile; correctness never depends on the browser staying open.
- **Server re-derives the amount** — the client sends only ids; `remaining` is computed from the installment; a paid/over-paid installment is rejected. No client-supplied amount is ever trusted.
- **Webhook auth = Stripe signature**, not cookie/JWT; the endpoint is the sole `[AllowAnonymous]` route; raw body is read for verification.
- **Exactly one tenant-filter bypass** — `PaymentRepository.GetByIntentIdIgnoringTenantAsync`, used only by reconcile (no JWT claims), always followed by a `SchoolId`-matches-verified-metadata assertion. Every other query stays tenant-scoped. Reconcile's save is Modified-only, so it never reads `CurrentSchoolId`.
- **Cross-child IDOR closed** — installment/payment ownership is asserted against `childId` (`FeeInvoice.StudentId == childId`) after the link guard.
- **Parent mutations are POST** (SameSite=Lax rule); parent identity from JWT `sub` only.
- **Overdue stays derived** — no `InstallmentStatus.Overdue` is ever written; no cron.
- **Cancel is blocked once paid** — no refund logic; the state stays coherent.
- **Balance/dashboard need no rework** — they already read `AmountPaid` (spec 20 / spec 16).

## Boundaries

- **Always:** re-derive the payable amount server-side; route parent calls through `ResolveLinkedChildOrThrow` *and* assert installment→child ownership; verify the Stripe signature on the webhook; verify the intent with Stripe on the return path (never trust the client); keep reconcile idempotent; keep the tenant-filter bypass to the single named repo method with a SchoolId assertion; POST for all mutations; run `dotnet test SchoolMgmt.slnx` and `npm run build` before done; keep Stripe secrets out of source control.
- **Ask first:** partial payments; refunds / unblocking Cancel; saved cards, wallets, or GCash; admin-recorded manual/cash payments; a receipt PDF/email; persisting `Overdue` or adding a reminder job; adding a payment collection to the shared `FeeInvoiceInstallment` entity/DTO.
- **Never:** mark an installment Paid from anywhere but `ReconcilePaymentAsync`; trust a client-supplied amount or a client "it succeeded" without re-verifying with Stripe; put payment mutation behind a GET; add `Stripe.net` to Application/Domain; bypass the tenant filter anywhere except the one named webhook repo method; skip the SchoolId-matches-metadata assertion; store live/secret Stripe keys in `appsettings.json`.

## Testing strategy

### Unit (`SchoolMgmt.Infrastructure.Tests` — hand-written fakes)

`PaymentService.ReconcilePaymentAsync` (fake `IPaymentRepository` returning a tracked `Payment`+installment, fake `IDateTimeProvider`):
- **Succeeded, Payment Pending** → installment `Status=Paid`, `AmountPaid=Amount`, `PaidAt` set; Payment `Succeeded`.
- **Same succeeded event twice** (call reconcile twice) → second call is a **no-op**; installment paid exactly once, one Succeeded Payment. *(idempotency — the load-bearing test.)*
- **Return path and webhook race** (Payment already Succeeded) → second reconcile returns without change.
- **Failed intent** → Payment `Failed` + `FailureReason`; installment untouched (still Pending).
- **Unknown intent id** → no-op, no throw.
- **SchoolId mismatch** (verified metadata ≠ Payment.SchoolId) → `DomainException`, nothing written.

`PaymentService.InitiateInstallmentPaymentAsync` (fake gateway returns a canned intent):
- **Issued invoice, unpaid installment** → creates Payment(Pending), calls gateway with `remaining*100` minor units + correct metadata, stores intent id, returns client secret + publishable key.
- **Already-paid installment** (`AmountPaid == Amount`) → `DomainException("already paid")`, no intent created.
- **Non-Issued invoice** (Draft/Cancelled) → `DomainException`.
- **Minor-units rounding** — e.g. `1234.56 → 123456`; half-away-from-zero on the boundary.

`FeeInvoiceService.CancelInvoiceAsync`:
- **Invoice with a Succeeded Payment** → `DomainException` (cancel blocked).
- **Invoice with only Pending/Failed Payments** → cancels normally.

`ParentPortalService`:
- **Unlinked child** on Pay/Confirm → `NotFoundException` (guard).
- **Installment/payment not belonging to the child** → `NotFoundException` (cross-child guard).

### Integration (`SchoolMgmt.IntegrationTests` — real Postgres via Testcontainers; **fake `IPaymentGateway`** registered in the test host so no real Stripe calls)

Seed: Parent-A linked to child A with an **Issued** invoice + installments; Parent-B linked to child B.
- Parent-A `POST .../children/{A}/installments/{instId}/pay` → 200 with client secret + a `Payment(Pending)` row persisted; fake gateway received correct minor-units + metadata.
- Parent-A `POST .../children/{A}/payments/{paymentId}/confirm` (fake gateway reports succeeded) → 204; installment now `Paid`; `GET .../children/{A}/fees` shows `TotalPaid` moved and the row `Paid`, overdue cleared.
- **Webhook** `POST /api/webhooks/stripe` with a fake-valid signature + succeeded event → 200; installment `Paid`. Posting the **same** event again → 200, still paid once (idempotent).
- **Webhook bad signature** → 400, no state change.
- **IDOR:** Parent-A paying child B's installment id → **404**. Parent-A confirming a payment id belonging to child B → **404**.
- Parent-A paying an **already-paid** installment → 400.
- **Cancel after pay:** admin cancels an invoice with a succeeded payment → 400 (blocked).
- **Auth matrix:** parent POSTs unauthenticated → 401; Admin/Teacher → 403; webhook works with **no** auth cookie (anonymous) given a valid signature.

### Frontend
- `PayInstallmentModal` renders the Elements form from a mocked `InitiatePaymentResult`; on a mocked `confirmPayment` success it invalidates the fee query and closes. `ChildFeesPage` shows a Pay button on unpaid/overdue rows and none on Paid rows. `npm run build` clean.

## Success criteria

- A parent can pay one Issued-invoice installment with Stripe test card `4242…`; the installment flips **Pending → Paid**, a `Payment(Succeeded)` row is recorded, and the balance hero + admin dashboard reflect the payment with no rework.
- `ReconcilePaymentAsync` is the only writer of Paid/Succeeded, is idempotent, and is exercised by both the webhook and the return path; replaying a webhook event does not double-pay.
- The webhook is anonymous, signature-verified, reads the raw body, resolves tenant from verified metadata via the single documented `IgnoreQueryFilters` path, and asserts SchoolId.
- Server re-derives the amount; client-supplied amounts and unverified success claims are never trusted; cross-child IDOR returns 404.
- An invoice with a succeeded payment cannot be Cancelled.
- No `InstallmentStatus.Overdue` is ever persisted; no cron; overdue stays derived (spec 20 unchanged).
- All unit + integration tests pass under `dotnet test SchoolMgmt.slnx`; frontend builds clean; no Stripe secret is committed.

## Resolved decisions (from idea doc 18)

- **Granularity:** one installment per transaction, full remaining amount.
- **Gateway:** Stripe test mode, integrated directly behind a thin `IPaymentGateway` seam (no multi-provider framework).
- **Confirmation:** webhook-authoritative + reconcile-on-return, both through one idempotent `ReconcilePaymentAsync`.
- **Record:** dedicated `Payment` entity (one row per intent; Failed rows kept) as the collections audit trail.
- **Webhook delivery:** publicly-reachable deploy receives Stripe events directly.
- **Card capture:** Stripe Elements inline (no redirect), chosen to fit the cookie-auth model.
- **Refunds:** out — Cancel is blocked once any Payment has Succeeded instead.
- **Minor-units conversion:** one helper in `PaymentService` (PHP × 100, half-away-from-zero).
```
