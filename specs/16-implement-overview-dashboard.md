# Spec 16 — Implement Overview Dashboard (Backend + Frontend)

## Related Docs & Prior Specs

- **Idea doc**: [docs/ideas/13-overview-dashboard.md](../docs/ideas/13-overview-dashboard.md)
- **Fee invoicing** (finance source — `FeeInvoice`, `FeeInvoiceInstallment`, statuses): [specs/12-implement-fee-invoicing.md](12-implement-fee-invoicing.md)
- **Attendance** (attendance source — `AttendanceRecord`, `AttendanceStatus`): [specs/14-implement-attendance-marking.md](14-implement-attendance-marking.md)
- **Class/section assignment** (enrollment + teacher source — `StudentSectionEnrollment`, `TeacherSectionSubject`): [specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md)
- **Academic year** (`IsCurrent`, `StartDate`/`EndDate`, selector default): [specs/03-implement-academic-year-term-configuration.md](03-implement-academic-year-term-configuration.md)
- **Student CRUD** (enrollment status lifecycle): [specs/05-implement-student-crud.md](05-implement-student-crud.md)
- **Multi-tenant base** (tenant scoping is automatic via EF global query filters): [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md)

## Overview

An **Admin-only** dashboard, served by a **single live aggregation endpoint** and rendered as the admin's post-login landing screen. It is organized around **signal over vanity**: finance is the hero with exception counts surfaced, attendance is the one genuine time-series line, enrollment is an honest breakdown (not a fake trend), and teachers are a coverage snapshot. Every tile drills through to its module page.

**Read-model only.** This feature adds **no new domain entities, no new tables, and no EF migration.** It composes read-only aggregations over data that already exists. That constraint drives every decision below.

**Scoping.** The whole screen is scoped to one **academic year**, chosen by a **year selector** that defaults to the year with `IsCurrent == true`. The endpoint takes `?academicYearId=` and falls back to the current year when omitted. Time-series charts use **monthly** buckets spanning the year's `StartDate`..`EndDate` (empty months render as zero — a real timeline, not just months that happen to have data).

---

## Part A — Backend

### A1. Endpoint

```
GET /api/dashboard/overview?academicYearId={guid?}
[Authorize(Roles = "Admin")]
```

- Read-only, no side effects (complies with the GET-must-be-side-effect-free rule in [.claude/rules/backend.md](../.claude/rules/backend.md)).
- `academicYearId` optional. Omitted → resolve the `IsCurrent` year. Supplied but not found (or not in this tenant) → **404**. No current year exists and none supplied → **404** with a clear message.
- Returns `200` with `DashboardOverviewDto`.
- Non-Admin authenticated user → **403**; unauthenticated → **401** (standard middleware behavior, same as every other Admin controller).
- Tenant scoping is automatic — the `AppDbContext` global query filter scopes every query by `SchoolId`. **Do not** filter `SchoolId` manually and **do not** bypass the filter.

### A2. Controller (thin)

```csharp
// WebApi/Controllers/DashboardController.cs
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Admin")]
public class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] Guid? academicYearId, CancellationToken ct)
        => Ok(await service.GetOverviewAsync(academicYearId, ct));
}
```

### A3. Application service

`Application/Dashboard/DashboardService.cs` — one service, read-only, **no `IUnitOfWork`** (nothing is persisted).

```csharp
public class DashboardService(
    IAcademicYearRepository yearRepo,     // resolve/validate the year (IsCurrent default)
    IDashboardRepository dashboard,       // read-model aggregation (A5)
    IDateTimeProvider clock)              // "today" for overdue; existing IDateTimeProvider
{
    public async Task<DashboardOverviewDto> GetOverviewAsync(Guid? academicYearId, CancellationToken ct)
    {
        var year = academicYearId is Guid id
            ? await yearRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException(...)
            : await yearRepo.GetCurrentAsync(ct) ?? throw new NotFoundException("No current academic year is set.");

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);   // IDateTimeProvider.UtcNow is a DateTimeOffset

        // Each of these is a pre-aggregated read from the repo — no entity graphs loaded into memory.
        var finance     = await dashboard.GetFinanceSummaryAsync(year.Id, today, ct);
        var financeMon  = await dashboard.GetMonthlyFinanceAsync(year.Id, year.StartDate, year.EndDate, ct);
        var attendance  = await dashboard.GetMonthlyAttendanceAsync(year.Id, year.StartDate, year.EndDate, ct);
        var enrollment  = await dashboard.GetEnrollmentBreakdownAsync(year.Id, ct);
        var teachers    = await dashboard.GetTeacherCoverageAsync(year.Id, ct);

        return new DashboardOverviewDto(year.Id, year.Name, finance, financeMon, attendance, enrollment, teachers);
    }
}
```

> **Note:** `IAcademicYearRepository.GetCurrentAsync` (returns the `IsCurrent` year) and `GetByIdAsync` (via `IRepository<AcademicYear>`) both already exist — reuse them, add nothing to the repo.
> **Note:** reuse the existing `IDateTimeProvider.UtcNow` (a `DateTimeOffset`) — never call `DateTime.UtcNow`/`DateTimeOffset.UtcNow` directly (keeps tests deterministic).

### A4. Response DTOs

`Application/Dashboard/Dtos/` — all `record`, status/enum values serialized as `string` (house convention).

```csharp
public record DashboardOverviewDto(
    Guid AcademicYearId,
    string AcademicYearName,
    FinanceSummaryDto Finance,
    IReadOnlyList<MonthlyMoneyPointDto> FinanceMonthly,
    IReadOnlyList<MonthlyAttendancePointDto> AttendanceMonthly,
    EnrollmentBreakdownDto Enrollment,
    TeacherCoverageDto Teachers);

// Finance — only ISSUED invoices count as billed obligations. Draft = not yet real; Cancelled = excluded.
public record FinanceSummaryDto(
    decimal Billed,          // Σ installment.Amount   where parent invoice Status = Issued
    decimal Collected,       // Σ installment.AmountPaid (coalesced 0) where parent invoice Status = Issued
    decimal Outstanding,     // Billed - Collected
    decimal Overdue,         // Σ (Amount - AmountPaid) where DueDate < today AND not fully paid AND invoice Issued
    decimal CollectionRate,  // Collected / Billed, 0 when Billed = 0 (guard divide-by-zero)
    int IssuedInvoiceCount,
    int DraftInvoiceCount);  // exception signal: billed value not yet issued

public record MonthlyMoneyPointDto(int Year, int Month, decimal Billed, decimal Collected);
//   Billed  = Σ installment.Amount    keyed on DueDate month (issued invoices)
//   Collected = Σ installment.AmountPaid keyed on PaidAt month  (issued invoices)

public record MonthlyAttendancePointDto(int Year, int Month, int TotalRecords, int PresentCount, double PresentRate);
//   PresentCount = Present + Late ; PresentRate = PresentCount / TotalRecords (0 when Total = 0)

public record EnrollmentBreakdownDto(
    int TotalEnrolled,                                 // distinct students with a section enrollment this year
    IReadOnlyList<GradeCountDto> ByGrade,              // grade-level name + count of enrollments
    IReadOnlyList<StatusCountDto> ByStatus);           // Student.EnrollmentStatus of enrolled students + count
public record GradeCountDto(Guid GradeId, string GradeName, int Count);
public record StatusCountDto(string Status, int Count);

// Teacher coverage — only what the schema can honestly compute (no section-subject curriculum map exists).
public record TeacherCoverageDto(
    int TeacherCount,                 // active teachers
    int AssignmentCount,              // TeacherSectionSubject rows for the year
    int SectionsWithEnrollments,      // sections that have >=1 student enrolled this year
    int SectionsWithoutAnyTeacher,    // GAP: section has students but zero TeacherSectionSubject rows
    int TeachersWithoutAssignment);   // GAP: active teacher with zero assignments this year
```

### A5. Read-model repository

`IDashboardRepository` in `Application/Dashboard/` (or `Application/Interfaces/`), implemented as `DashboardRepository` in `Infrastructure/Persistence/Repositories/`, registered in `Infrastructure/DependencyInjection.cs` (`AddScoped<IDashboardRepository, DashboardRepository>()`).

- **Deliberate deviation, called out:** this is a dedicated **read-model repository** that queries several `DbSet`s (`FeeInvoiceInstallment`, `AttendanceRecord`, `StudentSectionEnrollment`, `TeacherSectionSubject`, `Teacher`, `Section`) rather than one per entity. Justification: the alternative (bolting a bespoke aggregation method onto four existing single-entity repos and stitching the results in the service) scatters dashboard logic across unrelated repos for no benefit. It still honors the core repo rules — **read-only, never calls `SaveChanges`, never starts a transaction.** It just serves a query model that spans tables.
- Every method returns **already-aggregated** rows (DTOs or lightweight tuples), never entity graphs — the aggregation happens in SQL via `GroupBy`/`Sum`/`Count`, translated by EF Core. Do not pull rows into memory and aggregate in C#.
- Month bucketing: group by `(DueDate.Year, DueDate.Month)` etc. in the query, then in the service (or repo) **left-join against the full month sequence** from `StartDate`..`EndDate` so missing months appear as zero. Generate the month sequence in C# and merge — do not attempt a SQL calendar table.
- Overdue and "issued invoice" predicates are computed (see A4 comments). **Do not** read `InstallmentStatus.Overdue` — there is no job that keeps it current, so it is unreliable. Derive overdue from `DueDate < today` + unpaid + parent `Issued`.

### A6. DI registration

- `Application/DependencyInjection.cs`: `services.AddScoped<DashboardService>();`
- `Infrastructure/DependencyInjection.cs`: `services.AddScoped<IDashboardRepository, DashboardRepository>();`
- No validator (no request body). No new options, no new migration.

---

## Part B — Frontend

### B1. API client

`frontend/src/api/dashboard.ts` — mirror the `feeInvoices.ts` pattern (query-key factory + typed client + co-located interfaces).

```ts
export const DASHBOARD_KEYS = {
  overview: (yearId: string | null) => ['dashboard', 'overview', yearId] as const,
}
export const dashboardApi = {
  overview: (academicYearId?: string): Promise<DashboardOverviewDto> =>
    api.get('/dashboard/overview', { params: academicYearId ? { academicYearId } : undefined })
       .then((r) => r.data),
}
// TS interfaces mirroring the A4 DTOs (numbers for decimals, Status as string).
```

### B2. Charts dependency

- Add **`recharts`** to `frontend/package.json` and vendor the shadcn **chart** primitive at `frontend/src/components/ui/chart.tsx` (`ChartContainer` / `ChartTooltip` / `ChartTooltipContent`), per shadcn/ui. This is the charting choice already agreed in the idea doc.
- Verify-first step (see idea doc assumption): render one throwaway chart before building the tiles, to confirm recharts installs cleanly under React 19 / Vite.
- Use palette tokens from [docs/design-system.md](../docs/design-system.md) for series colors — no hard-coded hex.

### B3. Page composition & routing

- **`/dashboard` stays the shared landing route.** `DashboardPage.tsx` becomes a **role dispatcher**: `user.role === 'Admin'` → `<OverviewDashboard />`; otherwise the existing welcome message (Teacher/Parent dashboards are separate, un-built work — do not touch their behavior). This satisfies "replace the admin landing page" without a new route and without breaking non-admins.
- No new entry in `pages/admin/index.tsx` (the dashboard is not under `/admin/*`; it renders at `/dashboard` for admins). Backend authorization (`[Authorize(Roles="Admin")]`) is the real guard; the role dispatch is just UX.
- New files under `frontend/src/pages/dashboard/`:
  - `components/OverviewDashboard.tsx` — owns the year-selector state + the `useQuery` for the overview, lays out the tiles.
  - `components/YearSelector.tsx` — `Select` populated from the existing `academicYearsApi.list` (reuse `ACADEMIC_YEAR_KEYS`, `staleTime: Infinity`); default value = `years.find(y => y.isCurrent)?.id`. **Reuse the existing academic-years list endpoint — do not add years to the dashboard DTO.**
  - `components/FinanceTiles.tsx` — KPI cards (Collected, Outstanding, Overdue, Collection rate %) + draft-count exception line.
  - `components/FinanceChart.tsx` — monthly billed-vs-collected (bar or grouped bar).
  - `components/AttendanceChart.tsx` — monthly present-rate line.
  - `components/EnrollmentBreakdown.tsx` — total + by-grade bars + by-status counts.
  - `components/TeacherCoverage.tsx` — the coverage stats, gap counts styled as warnings when > 0.
- Layout: match existing admin pages — `div className="px-8 py-8 max-w-7xl mx-auto"`, `font-heading` title, responsive grid of cards (`grid gap-4 md:grid-cols-2`), `lucide-react` icons.

### B4. States & drill-through

- One `useQuery` for the whole screen keyed on the selected year (`DASHBOARD_KEYS.overview(yearId)`). While `isLoading`, render **per-tile skeletons**. On `isError`, a retryable inline error (reuse the existing axios-error extraction pattern).
- **Empty states** per tile (no issued invoices / no attendance records / no enrollments / no teachers) — a muted "No data yet for this year" rather than a broken chart.
- **Drill-through** (plain `<Link>`/`navigate`, GET navigation only):
  - Finance tiles → `/admin/fee-invoices?academicYearId={yearId}`
  - Attendance → `/admin/attendance`
  - Enrollment → `/admin/students`
  - Teachers → `/admin/teachers`

---

## Testing Strategy

### Backend — integration (`tests/SchoolMgmt.IntegrationTests/Dashboard/DashboardControllerTests.cs`)

`WebApplicationFactory` + Testcontainers Postgres, authenticate as demo Admin (existing `LoginAsAdminAsync` + `CookieTestHelpers`). Seed a known fixture (one year set current, a couple issued invoices with mixed paid/overdue installments, attendance across two months, enrollments across grades/statuses, teachers with/without assignments), then assert:

- **Auth**: unauthenticated → 401; Teacher → 403; Admin → 200.
- **Year resolution**: no `academicYearId` → uses the `IsCurrent` year; explicit valid id → that year; unknown id → 404.
- **Finance math**: `Billed`/`Collected`/`Outstanding`/`CollectionRate` match the seeded numbers; `Overdue` counts only past-due unpaid installments of *issued* invoices; draft-invoice installments excluded from `Billed`; `DraftInvoiceCount` correct; divide-by-zero → `CollectionRate == 0` when nothing billed.
- **Monthly series**: months with no data present as zero-valued points across the year's full `StartDate`..`EndDate` span; buckets keyed correctly (billed on `DueDate`, collected on `PaidAt`).
- **Attendance**: `PresentRate` = (Present+Late)/Total per month.
- **Enrollment**: `TotalEnrolled` distinct-student count; `ByGrade`/`ByStatus` sums reconcile.
- **Teacher coverage**: `SectionsWithoutAnyTeacher` and `TeachersWithoutAssignment` reflect the seeded gaps.

### Backend — unit (optional, only if pure logic is extracted)

If any bucket-merge / rate math lands in a pure helper (not SQL), cover it with xUnit + hand-written stubs (no Moq, plain `Assert.*`) — house convention.

### Frontend — Vitest + RTL

- `DashboardPage` dispatch: Admin renders `<OverviewDashboard />`; Teacher/Parent render the welcome message.
- `OverviewDashboard` with a mocked `dashboardApi.overview`: tiles render values; loading → skeletons; empty payload → per-tile empty states; year-selector change triggers a refetch with the new id. Recharts internals are not asserted deeply (render-without-crash is enough).

---

## Boundaries

**Always**
- Follow [.claude/rules/backend.md](../.claude/rules/backend.md): thin controller → Application service → read-only repository; DTOs as `record`s; `[Authorize(Roles="Admin")]` on the controller.
- Keep the endpoint a pure GET with zero side effects.
- Reuse the existing `IDateTimeProvider`, `academicYearsApi.list`, axios instance, and TanStack Query patterns.
- Compute overdue from dates, not from `InstallmentStatus.Overdue`.

**Ask first**
- Any change to the shared `/dashboard` route's behavior for **non-admin** roles.
- Any new backend dependency, or a charting library other than recharts.

**Never**
- Add a new table, entity, or EF migration for this feature — it is a read model.
- Add a background job / snapshot table (freshness is live-query, per idea doc & open question #91).
- Add a date-range picker or cross-filtering — the year selector is the only filter (idea doc "Not Doing").
- Manually filter `SchoolId` or bypass the tenant query filter.
- Mutate any state behind the GET endpoint.

---

## Out of Scope (deferred)

- Teacher / Parent dashboards (this is Admin-only).
- Export (PDF/CSV), auto-refresh / real-time push, custom/rearrangeable widgets.
- A cross-app persistent year selector in the top bar (local to this page for now).
