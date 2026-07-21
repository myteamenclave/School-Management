# Spec 20 — Implement Parent Portal: View Fee Balance / Invoices (Backend + Frontend)

## Related docs & prior specs

- **Idea doc**: [docs/ideas/17-parent-portal-fees.md](../docs/ideas/17-parent-portal-fees.md) — problem statement, the four resolved product decisions (Issued-only visibility, on-the-fly overdue, summary+schedule+line-items depth, Billed/Paid/Outstanding/Next-Due framing), assumptions, not-doing list, open questions.
- **Parent Portal — grades** ([specs/18-implement-parent-portal-grades.md](18-implement-parent-portal-grades.md)) — established the `api/parent` controller, `ParentPortalService`, the single `ResolveLinkedChildOrThrow(parentUserId, childId)` guard (unlinked/unknown child → **404**), `IStudentParentRepository.GetByUserIdAsync`, the JWT-`sub` caller identity, and the frontend parent shell (`ParentRoutes` nested routes, `AppShell` nav item, `parentPortal.ts`). This slice adds a **fourth child-scoped read** to that exact frame.
- **Parent Portal — attendance** ([specs/19-implement-parent-portal-attendance.md](19-implement-parent-portal-attendance.md)) — the direct pattern template: a child-scoped GET that runs the guard then returns a **server-computed summary** + a reused detail read, plus the shared `ParentChildYearBar` + `useParentChildYear` hook that both parent pages already consume. Fees reuse both unchanged. The *only* new shape is a fee summary (money) in place of attendance's rate.
- **Fee invoicing** ([specs/12-implement-fee-invoicing.md](12-implement-fee-invoicing.md) / [docs/ideas/08-fee-invoicing.md](../docs/ideas/08-fee-invoicing.md)) — provides `FeeInvoice` (+ `FeeInvoiceLineItem`, `FeeInvoiceInstallment`), `InvoiceStatus (Draft/Issued/Cancelled)`, `InstallmentStatus (Pending/Paid/Overdue)`, `FeeInvoiceService`, `FeeInvoiceDto` (line items + installments), `IFeeInvoiceRepository`. Installments already carry `Status`, `AmountPaid`, `PaidAt` — **payment-ready but always `Pending`/`null` today** (pay-online is a later slice). Doc 08 deferred "parent portal visibility" and "persisted overdue detection" — this spec delivers the visibility read and derives overdue *at read time only*.
- **Fee invoicing frontend** ([specs/13-implement-fee-invoicing-frontend.md](13-implement-fee-invoicing-frontend.md)) — the admin `FeeInvoicePage` detail view already renders line items + installments and defines a currency formatter (`Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' })`). The parent view reuses that formatting convention.
- **Auth** ([specs/02-implement-auth.md](02-implement-auth.md)) — JWT-cookie auth, `[Authorize(Roles = "Parent")]`, `User.FindFirstValue("sub")`.
- **Rules**: [.claude/rules/backend.md](../.claude/rules/backend.md) — thin controllers, one service per feature area, repositories touch only the `DbSet`, `IUnitOfWork` owns persistence, **GET must be read-only**, DI conventions, interfaces-where-consumed.

## Overview

A logged-in **Parent** opens the portal, picks a linked child and (optionally) an academic year, and views that child's fees: a **balance summary** (Billed / Paid / Outstanding / Next Due, plus an overdue callout) over the **Issued invoice** for that year — its installment schedule and its line-item breakdown. Everything is **read-only**.

The load-bearing rule is unchanged from specs 18/19: a parent may only ever see a child linked to them via `StudentParent`. The child read goes through the **same** `ResolveLinkedChildOrThrow` guard (unlinked/unknown child → **404**, never leak existence). No endpoint accepts an invoice id — the invoice is resolved from `(childId, year)` server-side.

Two things are genuinely new (everything else is reuse):
1. A **server-computed fee summary** — the balance/overdue rollup is a *business rule* with one authoritative home (`FeeInvoiceService`), reusable by the future pay-online module and the admin dashboard. This mirrors how attendance's rate lives in `AttendanceService`.
2. **On-the-fly overdue** — an installment is overdue when it is **not fully paid** and its `DueDate` is in the past (server clock). Display-only; no persisted `InstallmentStatus.Overdue` is written, no background job. (The stored `Overdue` enum value stays reserved for the payment module.)

**Scope decisions (from idea doc 17, all resolved):**
- **In:** `GET /api/parent/children/{childId}/fees?academicYearId=`; `FeeInvoiceService.GetStudentFeeOverviewAsync`; `StudentFeeSummaryDto` + `StudentFeeOverviewDto`; one `IFeeInvoiceRepository` method; frontend "Fees" nav item + `ChildFeesPage` (summary hero + installment schedule + line-item breakdown).
- **Out:** any payment / "Pay now" action (next module); Draft/Cancelled invoices; multi-invoice history; PDF/statement; persisted overdue/cron; notifications; parent home/dashboard.

---

## Part A — Backend

No new entity, no migration. New DTOs + one method on `FeeInvoiceService`, one method on `IFeeInvoiceRepository`, one method on `ParentPortalService`, one route on the existing controller.

### A1. `IFeeInvoiceRepository` — one addition

Existing `GetActiveForStudentAndYearAsync` returns the active (Draft **or** Issued) invoice *without details*. The parent view needs the **Issued** invoice **with** line items + installments (and Student/Template/Year for the shared `ToDetailDto`).

```csharp
// Application/FeeInvoices/IFeeInvoiceRepository.cs  (add one method)
public interface IFeeInvoiceRepository : IRepository<FeeInvoice>
{
    // ...existing methods unchanged...

    // NEW — the single Issued invoice for a student+year, with full details.
    Task<FeeInvoice?> GetIssuedForStudentAndYearWithDetailsAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);
}
```

Implementation (Infrastructure `FeeInvoiceRepository`), same `Include` set as `GetByIdWithDetailsAsync`:

```csharp
public Task<FeeInvoice?> GetIssuedForStudentAndYearWithDetailsAsync(
    Guid studentId, Guid academicYearId, CancellationToken ct = default) =>
    DbSet
        .Include(i => i.Student)
        .Include(i => i.FeeTemplate)
        .Include(i => i.AcademicYear)
        .Include(i => i.LineItems)
        .Include(i => i.Installments)
        .FirstOrDefaultAsync(i =>
            i.StudentId == studentId &&
            i.AcademicYearId == academicYearId &&
            i.Status == InvoiceStatus.Issued, ct);
```

The global `SchoolId` query filter applies automatically. Because the model guarantees at most one non-Cancelled invoice per student+year (`GetActiveForStudentAndYearAsync` semantics), filtering on `Status == Issued` returns **at most one row** — no ordering needed.

### A2. DTOs

Owned by the **fee feature** (the rollup rule lives there), in `Application/FeeInvoices/Dtos/`:

```csharp
// Application/FeeInvoices/Dtos/StudentFeeSummaryDto.cs
// Server-computed balance rollup for one student+year. All money is decimal.
// When there is no Issued invoice: HasInvoice=false, all amounts 0, all nullable fields null,
// OverdueInstallmentIds empty.
public record StudentFeeSummaryDto(
    bool HasInvoice,
    decimal TotalBilled,        // invoice.TotalAmount (sum of installment amounts)
    decimal TotalPaid,          // Σ (installment.AmountPaid ?? 0) — always 0 until pay-online
    decimal Outstanding,        // TotalBilled - TotalPaid
    DateOnly? NextDueDate,      // earliest UNPAID installment's DueDate (nulls-last); null if none
    decimal? NextDueAmount,     // that installment's remaining amount; null if none
    decimal OverdueAmount,      // Σ remaining amount of installments past due and not fully paid
    int OverdueCount,           // count of those installments
    List<Guid> OverdueInstallmentIds);  // ids the UI highlights as past-due (one overdue definition)

// Application/FeeInvoices/Dtos/StudentFeeOverviewDto.cs
// The parent fee payload: the summary hero + the Issued invoice (null when none issued).
// Invoice reuses the existing FeeInvoiceDto (line items + installments) UNCHANGED.
public record StudentFeeOverviewDto(
    StudentFeeSummaryDto Summary,
    FeeInvoiceDto? Invoice);
```

`FeeInvoiceDto` / `FeeInvoiceLineItemDto` / `FeeInvoiceInstallmentDto` are reused **unchanged** — overdue is surfaced via `Summary.OverdueInstallmentIds`, so no field is added to the shared installment DTO and the admin path is untouched (resolves idea-doc open question: keep the rule in the summary, don't fork the DTO).

### A3. `FeeInvoiceService.GetStudentFeeOverviewAsync`

One method, fetches the Issued invoice **once**, computes the summary and maps the detail from the same entity. `IDateTimeProvider` is already injected into `FeeInvoiceService`.

```csharp
public async Task<StudentFeeOverviewDto> GetStudentFeeOverviewAsync(
    Guid studentId, Guid academicYearId, CancellationToken ct = default)
{
    var invoice = await invoiceRepo.GetIssuedForStudentAndYearWithDetailsAsync(
        studentId, academicYearId, ct);

    if (invoice is null)
        return new StudentFeeOverviewDto(
            new StudentFeeSummaryDto(false, 0m, 0m, 0m, null, null, 0m, 0, []),
            null);

    var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

    var totalBilled = invoice.TotalAmount;
    var totalPaid = invoice.Installments.Sum(i => i.AmountPaid ?? 0m);
    var outstanding = totalBilled - totalPaid;

    // "Unpaid" = remaining > 0; robust to future partial payments.
    static decimal Remaining(FeeInvoiceInstallment i) => i.Amount - (i.AmountPaid ?? 0m);

    var overdue = invoice.Installments
        .Where(i => Remaining(i) > 0m && i.DueDate.HasValue && i.DueDate.Value < today)
        .ToList();

    // Next due = earliest unpaid installment by DueDate (nulls last). Naturally surfaces an
    // overdue installment first if one exists; null when everything is paid / has no due date.
    var nextDue = invoice.Installments
        .Where(i => Remaining(i) > 0m && i.DueDate.HasValue)
        .OrderBy(i => i.DueDate!.Value)
        .FirstOrDefault();

    var summary = new StudentFeeSummaryDto(
        HasInvoice: true,
        TotalBilled: totalBilled,
        TotalPaid: totalPaid,
        Outstanding: outstanding,
        NextDueDate: nextDue?.DueDate,
        NextDueAmount: nextDue is null ? null : Remaining(nextDue),
        OverdueAmount: overdue.Sum(Remaining),
        OverdueCount: overdue.Count,
        OverdueInstallmentIds: overdue.Select(i => i.Id).ToList());

    return new StudentFeeOverviewDto(summary, ToDetailDto(invoice));
}
```

Notes:
- Reuses the existing private `ToDetailDto(FeeInvoice)` for the invoice mapping — no duplicate mapping logic.
- Overdue uses the server clock (`IDateTimeProvider.UtcNow`) reduced to a UTC `DateOnly`. Acceptable for the demo; documented as a known simplification (no per-tenant timezone).
- Purely read-only — no `SaveChanges`, no transaction.

### A4. `ParentPortalService.GetChildFeesAsync`

Add `IFeeInvoiceRepository`? No — the summary lives in `FeeInvoiceService`, so **inject `FeeInvoiceService`** into `ParentPortalService` (same as it already injects `GradebookService` and `AttendanceService`). The method is guard → delegate.

```csharp
// constructor: add  FeeInvoiceService fees
public async Task<StudentFeeOverviewDto> GetChildFeesAsync(
    Guid parentUserId, Guid childId, Guid? academicYearId, CancellationToken ct = default)
{
    await ResolveLinkedChildOrThrow(parentUserId, childId, ct);   // AUTHORIZATION

    var yearId = academicYearId
        ?? (await years.GetCurrentAsync(ct))?.Id
        ?? throw new NotFoundException("No current academic year is set.");

    return await fees.GetStudentFeeOverviewAsync(childId, yearId, ct);
}
```

Identical shape to `GetChildGradesAsync` / `GetChildAttendanceAsync`: guard first, default the year to current, delegate to the owning feature service.

### A5. `ParentPortalController` — one route

Add to the existing controller (Parent-only, GET/read-only):

```csharp
// GET /api/parent/children/{childId}/fees?academicYearId=
[HttpGet("children/{childId:guid}/fees")]
public async Task<IActionResult> GetChildFees(
    Guid childId, [FromQuery] Guid? academicYearId, CancellationToken ct)
    => Ok(await service.GetChildFeesAsync(ParentUserId, childId, academicYearId, ct));
```

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `GET` | `/api/parent/children/{childId}/fees` | `GetChildFeesAsync` | 200 — `StudentFeeOverviewDto` | **404** if child not linked to caller; `academicYearId` optional (defaults to current); `Invoice` null + zeroed summary when no Issued invoice for the year |

No `DomainExceptionFilter` change — `NotFoundException`→404 already mapped.

### A6. DI registration

No new registration needed for the controller/service wiring itself, but `ParentPortalService` now depends on `FeeInvoiceService`. `FeeInvoiceService` is already registered (spec 12) and `ParentPortalService` (spec 18); both are `Scoped`. Confirm `FeeInvoiceService` is registered as a concrete type resolvable by `ParentPortalService` (it is — same pattern as `GradebookService`/`AttendanceService` injected into `ParentPortalService`). No Infrastructure DI change (only a method added to the existing `FeeInvoiceRepository`).

### A7. Project structure (backend)

```
backend/
  SchoolMgmt.Application/
    FeeInvoices/
      IFeeInvoiceRepository.cs                 # modified — add GetIssuedForStudentAndYearWithDetailsAsync
      FeeInvoiceService.cs                     # modified — add GetStudentFeeOverviewAsync
      Dtos/
        StudentFeeSummaryDto.cs                # new
        StudentFeeOverviewDto.cs               # new
    ParentPortal/
      ParentPortalService.cs                   # modified — inject FeeInvoiceService, add GetChildFeesAsync

  SchoolMgmt.Infrastructure/
    Persistence/Repositories/
      FeeInvoiceRepository.cs                  # modified — implement the new method

  SchoolMgmt.WebApi/
    Controllers/
      ParentPortalController.cs                # modified — add fees route
```

---

## Part B — Frontend

### B1. API client (`parentPortal.ts`)

Add the fee types, a query key, and the fetch — matching the existing attendance additions.

```ts
export interface FeeInvoiceLineItem {
  id: string
  name: string
  originalAmount: number
  discountAmount: number
  finalAmount: number
  displayOrder: number
}

export interface FeeInvoiceInstallment {
  id: string
  name: string
  percentage: number
  dueDate: string | null   // "yyyy-MM-dd"
  amount: number
  status: string           // stored status ("Pending"); overdue comes from the summary
  displayOrder: number
}

// Reuses the admin FeeInvoiceDto shape (spec 13), read-only for the parent.
export interface FeeInvoice {
  id: string
  invoiceCode: string
  studentId: string; studentName: string; studentCode: string
  academicYearId: string; academicYearName: string
  feeTemplateId: string; templateName: string
  totalAmount: number
  status: string
  issuedAt: string | null
  cancelledAt: string | null
  createdAt: string
  updatedAt: string | null
  lineItems: FeeInvoiceLineItem[]
  installments: FeeInvoiceInstallment[]
}

export interface StudentFeeSummary {
  hasInvoice: boolean
  totalBilled: number
  totalPaid: number
  outstanding: number
  nextDueDate: string | null
  nextDueAmount: number | null
  overdueAmount: number
  overdueCount: number
  overdueInstallmentIds: string[]
}

export interface StudentFeeOverview {
  summary: StudentFeeSummary
  invoice: FeeInvoice | null
}

// add to PARENT_KEYS:
//   childFees: (childId, academicYearId) => ['parent', 'fees', childId, academicYearId] as const,

// add to parentPortalApi:
//   getChildFees: (childId, academicYearId?) =>
//     api.get<StudentFeeOverview>(`/parent/children/${childId}/fees`, {
//       params: academicYearId ? { academicYearId } : undefined,
//     }).then((r) => r.data),
```

### B2. Routes + nav

- `frontend/src/pages/parent/index.tsx` — add `<Route path="fees" element={<ChildFeesPage />} />`.
- `frontend/src/layouts/AppShell.tsx` — add a fourth Parent nav item: label **"Fees"**, `to: '/parent/fees'`, `roles: ['Parent']`, icon `Wallet` (import from `lucide-react` alongside the existing icons).

### B3. `ChildFeesPage`

**Route**: `/parent/fees` · **Role**: Parent only. File: `frontend/src/pages/parent/fees/ChildFeesPage.tsx`. Structure mirrors `ChildAttendancePage`:

1. Use `useParentChildYear()` for children/years/selection + loading/error.
2. Render `ParentChildYearBar` (unchanged).
3. Fee query — `parentPortalApi.getChildFees(childId, academicYearId)`, `enabled` once both selected; key `PARENT_KEYS.childFees(...)`.
4. **Empty state** when `!data?.summary.hasInvoice` (or `invoice === null`): "No fees have been issued for {child} in {year} yet."
5. **Summary hero** — reuse the `StatCard` pattern: **Outstanding** (primary/emphasis), **Billed**, **Paid**, **Next Due** (date + amount, or "—"). When `overdueCount > 0`, a red callout: "{overdueCount} installment(s) past due · {overdueAmount}".
6. **Installment schedule table** — columns: Installment (name) · Due Date · Amount · Status. A row whose id ∈ `summary.overdueInstallmentIds` gets a red "Overdue" badge/row treatment (do **not** recompute the rule client-side — use the id set). Order by `displayOrder`.
7. **Line-item breakdown table** — columns: Item · Original · Discount · Final. Order by `displayOrder`. Footer/total = `invoice.totalAmount`.
8. **Currency** — format with `Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' })`, matching the admin invoice pages (spec 13). Replicate the same `currencyFmt` const the admin `FeeInvoicePage` uses (or extract a tiny shared helper if trivially clean — otherwise replicate to avoid cross-module coupling).
9. **Loading/error** — reuse the page's `EmptyState`/loading treatment from `ChildAttendancePage`.

### B4. Project structure (frontend)

```
frontend/src/
  api/parentPortal.ts                          # modified — fee types, key, getChildFees
  pages/parent/
    index.tsx                                  # modified — add fees route
    fees/
      ChildFeesPage.tsx                        # new
  layouts/AppShell.tsx                         # modified — add "Fees" nav item (Wallet icon)
```

---

## Implementation order

1. **Backend**: `IFeeInvoiceRepository` method + impl → `StudentFeeSummaryDto`/`StudentFeeOverviewDto` → `FeeInvoiceService.GetStudentFeeOverviewAsync` → `ParentPortalService` (inject `FeeInvoiceService` + `GetChildFeesAsync`) → controller route. Build + `dotnet test SchoolMgmt.slnx`.
2. **Frontend**: `parentPortal.ts` additions → route + AppShell nav → `ChildFeesPage` (hero → installment schedule → line items → empty/loading states). `npm run build`.

Commit after backend-complete and after the frontend page.

---

## Key invariants

- **Single authorization surface** — the fee read passes through `ResolveLinkedChildOrThrow`; the invoice is resolved from `(childId, year)`, never a client-supplied invoice id. Unlinked/unknown child → **404** (never 403, never leak existence).
- **Parent identity from JWT only** — `parentUserId` is `User.FindFirstValue("sub")`, never a body/route field.
- **Issued only** — the parent never sees Draft or Cancelled invoices; `GetIssuedForStudentAndYearWithDetailsAsync` filters `Status == Issued`.
- **One server-side money rule** — Billed/Paid/Outstanding/Next-Due/Overdue are computed only in `FeeInvoiceService.GetStudentFeeOverviewAsync`; the frontend never recomputes them (overdue rows come from `OverdueInstallmentIds`).
- **Overdue is derived, never persisted** — read-time only, using `IDateTimeProvider`; no `InstallmentStatus.Overdue` is written, no background job.
- **Reuse, don't fork** — `FeeInvoiceDto` (+ line item / installment DTOs) and `ToDetailDto` are reused unchanged; only the summary + overview DTOs are new. Shared admin invoice DTO/path untouched.
- **All parent endpoints GET/read-only** (`SameSite=Lax` CSRF rule); no parent write path.
- **Tenant filter never bypassed** — all reads respect the global `SchoolId` filter; no `IgnoreQueryFilters`.
- **No new entity, no migration** — read composition over existing tables.
- **Children always listed** regardless of `EnrollmentStatus` (status badged, not filtered — the `StudentParent` link is the authorization truth).

## Boundaries

- **Always:** derive the parent from the JWT; route the fee read through the link guard; return 404 for an unlinked child; keep the endpoint GET/read-only; compute the balance/overdue rollup server-side; run `dotnet test SchoolMgmt.slnx` before done.
- **Ask first:** exposing Draft/Cancelled invoices to parents; adding a "Pay now" / payment path (that's the next module); adding a per-installment `IsOverdue` field to the *shared* `FeeInvoiceInstallmentDto` (this spec keeps overdue in the summary); showing multi-year fee history; a parent home/dashboard landing.
- **Never:** add Parent to `api/fee-invoices` or accept an invoice id from a parent request (dedicated `api/parent` controller + link guard only); write a persisted `Overdue` status or run a cron from this slice; call `SaveChanges`/transactions (read-only); bypass the tenant query filter; recompute the money rules on the client.

## Testing strategy

### Unit (`SchoolMgmt.Infrastructure.Tests` — hand-written fakes)

`FeeInvoiceService.GetStudentFeeOverviewAsync` (fake `IFeeInvoiceRepository` + fake `IDateTimeProvider` for deterministic "today"):
- **No Issued invoice** → `HasInvoice=false`, all amounts 0, `Invoice` null, `OverdueInstallmentIds` empty.
- **Issued, nothing paid, all future due dates** → `TotalPaid=0`, `Outstanding=TotalBilled`, `OverdueCount=0`, `NextDue` = earliest installment.
- **Issued with a past-due unpaid installment** → that installment appears in `OverdueInstallmentIds`, `OverdueCount=1`, `OverdueAmount` = its remaining amount; `NextDueDate` = the overdue (earliest unpaid) date.
- **Partially paid installment (`AmountPaid < Amount`)** → still counts as unpaid; `Remaining` used for Outstanding/Overdue/NextDue amounts (payment-ready math).
- **Fully paid installment (`AmountPaid == Amount`)** → excluded from overdue and next-due; contributes to `TotalPaid`.
- **Installment with null `DueDate`** → never overdue; excluded from next-due ordering.

`ParentPortalService.GetChildFeesAsync`:
- **Linked child** → delegates to `GetStudentFeeOverviewAsync(childId, yearId)`; omitting `academicYearId` uses `GetCurrentAsync().Id`.
- **Unlinked / unknown child** → `NotFoundException` (guard via `GetLinkAsync(childId, parentUserId)`).
- **No current year and no `academicYearId`** → `NotFoundException("No current academic year is set.")`.

### Integration (`SchoolMgmt.IntegrationTests` — real Postgres via Testcontainers)

Seed: Parent-A linked to child A (with an **Issued** invoice + installments), a Draft invoice for a different year, a second unrelated Parent-B linked to child B.
- Parent-A `GET /api/parent/children/{A}/fees` → 200, `HasInvoice=true`, summary numbers match the seeded invoice; `Invoice` populated with line items + installments.
- Parent-A on a year where A has only a **Draft** (or **Cancelled**) invoice → 200 with `HasInvoice=false`, `Invoice=null` (Issued-only proven).
- **IDOR:** Parent-A `GET /api/parent/children/{B}/fees` → **404**.
- Parent-A `GET /api/parent/children/{unknownGuid}/fees` → 404.
- **Auth matrix:** unauthenticated → 401; Admin/Teacher → 403 (Parent-only).
- Omitting `academicYearId` returns the current year's overview.

### Frontend
- `ChildFeesPage` renders the hero, installment schedule, and line-item breakdown from a mocked `StudentFeeOverview`; overdue rows are flagged from `overdueInstallmentIds`; the no-invoice empty state renders when `hasInvoice=false`. `npm run build` clean.

## Success criteria

- `IFeeInvoiceRepository.GetIssuedForStudentAndYearWithDetailsAsync` returns the single Issued invoice (with details) for a student+year, tenant-scoped, or null.
- `FeeInvoiceService.GetStudentFeeOverviewAsync` returns a correct server-computed summary (Billed/Paid/Outstanding/Next-Due/Overdue) + the Issued `FeeInvoiceDto`, or a zeroed summary + null invoice when none is issued.
- `GET /api/parent/children/{childId}/fees` returns that child's fee overview (current year by default, or the requested year), **404s** for any child not linked to the caller, and never exposes Draft/Cancelled invoices.
- All `api/parent/*` endpoints remain GET, 401 unauthenticated, 403 for non-Parent roles.
- Parent frontend: "Fees" nav item → `/parent/fees`; summary hero (Outstanding/Billed/Paid/Next-Due + overdue callout); installment schedule with overdue rows flagged; line-item breakdown; currency in `en-PH`/PHP; real empty state for no-issued-invoice.
- Overdue is derived at read time only; no persisted `Overdue` status is written and no client-side money rule exists.
- All unit and integration tests pass under `dotnet test SchoolMgmt.slnx`; frontend builds clean.

## Resolved decisions (from idea doc 17)

- **Visibility — Issued only.** Drafts (admin scratch) and Cancelled (voided) are never parent-visible; resolves to at most one invoice per child per year.
- **Overdue — on-the-fly at read time** (`remaining > 0 && DueDate < today`, server clock); display-only, surfaced via `OverdueInstallmentIds`; no persisted state, no cron.
- **Detail depth — summary + installment schedule + line items** (reuse `FeeInvoiceDto` whole).
- **Balance framing — Billed / Paid / Outstanding + Next Due**; `Paid`/`Outstanding` are payment-ready (`Paid=0` today).
- **`IsOverdue` placement — in the summary (`OverdueInstallmentIds`), not on the shared installment DTO** — keeps one overdue definition server-side and leaves the admin invoice DTO/path untouched.
