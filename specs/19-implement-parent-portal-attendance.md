# Spec 19 — Implement Parent Portal: View Child's Attendance (Backend + Frontend)

## Related docs & prior specs

- **Idea doc**: [docs/ideas/16-parent-portal-attendance.md](../docs/ideas/16-parent-portal-attendance.md) — problem statement, "summary + log" depth decision, **server-side summary** decision, the resolved attendance-rate formula, "two nav items" navigation decision.
- **Parent Portal — grades** ([specs/18-implement-parent-portal-grades.md](18-implement-parent-portal-grades.md)) — the pattern this spec copies. It established the `api/parent` controller, `ParentPortalService`, the single `ResolveLinkedChildOrThrow(parentUserId, childId)` guard (unlinked/unknown → 404), `IStudentParentRepository.GetByUserIdAsync`/`GetLinkAsync`, the frontend parent shell (`ParentRoutes`, `parentPortal.ts`, child switcher + year selector inline in `ChildGradesPage`), and the AppShell "My Children" nav item. **This spec adds a second read to that exact frame — no new auth surface.**
- **Attendance marking** ([specs/14-implement-attendance-marking.md](14-implement-attendance-marking.md)) — provides `AttendanceService`, `AttendanceRecord`, `AttendanceStatus` enum (Present/Late/Absent/Excused), `IAttendanceRepository.GetByStudentAndYearAsync` (Section included), and the existing `AttendanceService.GetStudentHistoryAsync(studentId, academicYearId)` → `List<AttendanceHistoryEntryDto>` read, built explicitly "for parent portal and dashboard." This slice **reuses that history read unchanged** for the log and **adds** a summary method beside it.
- **Class/section assignment** ([specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md)) — `IStudentSectionEnrollmentRepository`, reused via `ParentPortalService` (children labels), unchanged here.
- **Academic year / term** ([specs/03-implement-academic-year-term-configuration.md](03-implement-academic-year-term-configuration.md)) — `IAcademicYearRepository.GetCurrentAsync()`, used to default the year when `academicYearId` is omitted (same as grades).
- **Auth** ([specs/02-implement-auth.md](02-implement-auth.md)) — JWT-cookie auth, `[Authorize(Roles = "Parent")]`, `User.FindFirstValue("sub")` for the caller.
- **Rules**: [.claude/rules/backend.md](../.claude/rules/backend.md) — thin controllers, one service per feature area, repositories touch only the `DbSet`, `IUnitOfWork` owns persistence, **GET must be read-only**, DI conventions, interfaces-where-consumed.

## Overview

A logged-in **Parent** picks one of their linked children and a year, and sees that child's attendance for the year: a **summary hero** (attendance rate + the four per-status counts) over a **reverse-chronological daily log** (date · status · notes). Everything is **read-only** and reached through the existing parent shell.

The load-bearing rule is unchanged from spec 18: a parent may only ever read a child linked to them via `StudentParent`. The new endpoint routes through the **same** `ResolveLinkedChildOrThrow` guard — unlinked or unknown child → **404**, never leak existence, never trust a client-supplied student id, parent identity always from the JWT `sub`.

The one genuinely new thing versus the grades slice: **grades arrived pre-summarized (server computes Term + Letter); attendance does not.** So this spec adds a server-computed attendance summary. It is computed **server-side on purpose** — the attendance-rate formula is a business rule that must have one authoritative definition (reusable by the future dashboard), not an ad-hoc `.filter()` in React.

**Scope decisions (from idea doc 16):**
- **In:** `GET /api/parent/children/{childId}/attendance?academicYearId=` (combined summary + log); a server-side summary method on `AttendanceService`; second parent nav item + `ChildAttendancePage`; extract the child+year selectors from `ChildGradesPage` into a shared `ParentChildYearBar`.
- **Out:** per-subject/period attendance, any write path, absence notifications, attendance trend charts, pagination, a tabbed parent page or parent-home landing.

**The attendance-rate formula (resolved, idea doc 16):** `rate = (Present + Late) ÷ TotalMarked`. Absent and Excused both count against the rate; all four counts are always shown. Zero marked days → rate is `null` ("—"), never divide-by-zero.

---

## Part A — Backend

No new entities, no migration. One new DTO file, one new method on `AttendanceService`, one new method on `ParentPortalService`, one new endpoint on the existing `ParentPortalController`.

### A1. Repositories — no changes

`IAttendanceRepository.GetByStudentAndYearAsync(studentId, academicYearId)` (Section included) already exists and is exactly what both the log and the summary need. `IStudentParentRepository.GetLinkAsync` (the guard) and `GetByUserIdAsync` (the children list) already exist from spec 18. **No repository interface or implementation changes in this spec.**

### A2. DTOs

Add the summary DTO and the combined parent-attendance response. The daily-log rows reuse **`AttendanceHistoryEntryDto`** (spec 14) unchanged.

```csharp
// Application/Attendance/Dtos/AttendanceDtos.cs  (append to the existing file)

// Per-year attendance summary for a single student. Rate is (Present + Late) / TotalMarked,
// expressed 0–100 (one decimal at the edge, formatting is the client's job); null when
// TotalMarked == 0 (no days marked yet — never divide-by-zero).
public record StudentAttendanceSummaryDto(
    int TotalMarked,
    int PresentCount,
    int LateCount,
    int AbsentCount,
    int ExcusedCount,
    decimal? AttendanceRate      // 0–100, or null when TotalMarked == 0
);
```

```csharp
// Application/ParentPortal/Dtos/ParentAttendanceDto.cs  (new)
using SchoolMgmt.Application.Attendance.Dtos;

// Combined payload for the parent attendance view: the year summary (hero) plus the
// full reverse-chronological daily log (reusing the spec-14 history row DTO unchanged).
public record ParentAttendanceDto(
    StudentAttendanceSummaryDto Summary,
    List<AttendanceHistoryEntryDto> Entries
);
```

`StudentAttendanceSummaryDto` lives in `Application/Attendance/Dtos` (not ParentPortal) because the rate rule is owned by the attendance feature and will be reused by the dashboard.

### A3. `AttendanceService.GetStudentSummaryAsync` — new method

The rate formula lives here, beside the existing `GetStudentHistoryAsync`, so the attendance feature owns its own business rule.

```csharp
// Application/Attendance/AttendanceService.cs  (add one method)

public async Task<StudentAttendanceSummaryDto> GetStudentSummaryAsync(
    Guid studentId, Guid academicYearId, CancellationToken ct = default)
{
    var records = await attendanceRepo.GetByStudentAndYearAsync(studentId, academicYearId, ct);

    var present = records.Count(r => r.Status == AttendanceStatus.Present);
    var late    = records.Count(r => r.Status == AttendanceStatus.Late);
    var absent  = records.Count(r => r.Status == AttendanceStatus.Absent);
    var excused = records.Count(r => r.Status == AttendanceStatus.Excused);
    var total   = records.Count;

    // Rate = "physically in class" (Present + Late) over all marked days.
    // Absent AND Excused count against it. Null when nothing is marked (no divide-by-zero).
    decimal? rate = total == 0
        ? null
        : Math.Round((decimal)(present + late) * 100 / total, 1);

    return new StudentAttendanceSummaryDto(total, present, late, absent, excused, rate);
}
```

Note: `AttendanceService` already injects `IAttendanceRepository attendanceRepo` and `using`s `SchoolMgmt.Domain.Enums` (`AttendanceStatus`) — no new dependencies. This method is read-only; it does **not** call `IUnitOfWork.SaveChangesAsync`.

### A4. `ParentPortalService.GetChildAttendanceAsync` — new method

`ParentPortalService` orchestrates: guard → resolve year → fetch summary + log → combine. It gains one constructor dependency: `AttendanceService`.

```csharp
// Application/ParentPortal/ParentPortalService.cs

// constructor — add AttendanceService alongside the existing GradebookService:
public class ParentPortalService(
    IStudentParentRepository links,
    IStudentSectionEnrollmentRepository enrollments,
    IAcademicYearRepository years,
    GradebookService gradebook,
    AttendanceService attendance)          // NEW
{
    // ... GetMyChildrenAsync, GetChildGradesAsync, GetAcademicYearsAsync, ResolveLinkedChildOrThrow unchanged ...

    // Attendance (summary + daily log) for one linked child. academicYearId null => current year.
    public async Task<ParentAttendanceDto> GetChildAttendanceAsync(
        Guid parentUserId, Guid childId, Guid? academicYearId, CancellationToken ct = default)
    {
        await ResolveLinkedChildOrThrow(parentUserId, childId, ct);   // AUTHORIZATION — same single surface

        var yearId = academicYearId
            ?? (await years.GetCurrentAsync(ct))?.Id
            ?? throw new NotFoundException("No current academic year is set.");

        var summary = await attendance.GetStudentSummaryAsync(childId, yearId, ct);
        var entries = await attendance.GetStudentHistoryAsync(childId, yearId, ct);   // reused unchanged
        return new ParentAttendanceDto(summary, entries);
    }
}
```

Mirrors `GetChildGradesAsync` exactly: guard first, then the current-year default fallback, then the read. The guard already proved the child is linked, so the reads are keyed by `childId` within the tenant filter — no second parent re-check.

`GetStudentHistoryAsync` returns records already ordered by the repository; if it is not descending-by-date, the frontend sorts for display (see B3). (Do **not** change the shared history method's ordering — the admin/teacher student-history view depends on it. Sort in the parent UI.)

### A5. `ParentPortalController` — one new endpoint

Append to the existing controller. Parent-only, GET, read-only.

```csharp
// WebApi/Controllers/ParentPortalController.cs  (add one action)

// GET /api/parent/children/{childId}/attendance?academicYearId=
[HttpGet("children/{childId:guid}/attendance")]
public async Task<IActionResult> GetChildAttendance(
    Guid childId, [FromQuery] Guid? academicYearId, CancellationToken ct)
    => Ok(await service.GetChildAttendanceAsync(ParentUserId, childId, academicYearId, ct));
```

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `GET` | `/api/parent/children/{childId}/attendance` | `GetChildAttendanceAsync` | 200 — `ParentAttendanceDto` | **404** if child not linked to caller; `academicYearId` optional (defaults to current) |

No `DomainExceptionFilter` change — `NotFoundException`→404 already mapped.

### A6. DI registration

`ParentPortalService` now depends on `AttendanceService`. Both are already registered as `Scoped` (spec 14 registers `AttendanceService`; spec 18 registers `ParentPortalService`). **Verify** `AttendanceService` is registered in `Application/DependencyInjection.cs`; if it is, no DI change is needed. No Infrastructure DI change.

### A7. Project structure (backend)

```
backend/
  SchoolMgmt.Application/
    Attendance/
      AttendanceService.cs                  # modified — add GetStudentSummaryAsync
      Dtos/AttendanceDtos.cs                # modified — add StudentAttendanceSummaryDto
    ParentPortal/
      ParentPortalService.cs                # modified — inject AttendanceService, add GetChildAttendanceAsync
      Dtos/
        ParentAttendanceDto.cs              # new
  SchoolMgmt.WebApi/
    Controllers/
      ParentPortalController.cs             # modified — add GetChildAttendance action
```

---

## Part B — Frontend

### B1. API client — extend `parentPortal.ts`

```ts
// frontend/src/api/parentPortal.ts  (add)

export interface AttendanceHistoryEntry {
  id: string
  sectionId: string
  sectionName: string
  date: string            // ISO date (DateOnly serializes as "yyyy-MM-dd")
  status: 'Present' | 'Late' | 'Absent' | 'Excused'
  notes: string | null
}

export interface StudentAttendanceSummary {
  totalMarked: number
  presentCount: number
  lateCount: number
  absentCount: number
  excusedCount: number
  attendanceRate: number | null   // 0–100, or null when nothing marked
}

export interface ParentAttendance {
  summary: StudentAttendanceSummary
  entries: AttendanceHistoryEntry[]
}

// add to PARENT_KEYS:
//   childAttendance: (childId: string, academicYearId: string) =>
//     ['parent', 'attendance', childId, academicYearId] as const,

// add to parentPortalApi:
//   getChildAttendance: (childId: string, academicYearId?: string) =>
//     api.get<ParentAttendance>(`/parent/children/${childId}/attendance`, {
//       params: academicYearId ? { academicYearId } : undefined,
//     }).then((r) => r.data),
```

### B2. Extract `ParentChildYearBar` (shared shell) — refactor

Spec 18 built the child switcher + year selector **inline** in `ChildGradesPage`. Now that a second page needs the identical controls, extract them so they aren't copy-pasted.

```
frontend/src/pages/parent/ParentChildYearBar.tsx   # new
```

The component owns the two `Select`s and the "no children linked / loading / error" bootstrap, and lifts selection to the parent via props. Suggested shape:

```tsx
export interface ParentChildYearBarProps {
  children: ParentChild[]
  years: ParentAcademicYear[]
  childId: string | null
  academicYearId: string | null
  onChildChange: (id: string) => void
  onYearChange: (id: string) => void
}
```

Move the child-switcher `<Select>` (hidden when a single child), the year `<Select>`, and the selected-child summary line (name · code · grade/section badge · non-Active status badge) from `ChildGradesPage` into this component. Keep the **auto-select-single-child** and **default-to-current-year** `useEffect`s where the selection state lives (either lift them into a shared `useParentChildYear()` hook, or keep them in each page — a small hook is cleaner and is the recommended path since both pages need identical defaulting).

**Refactor `ChildGradesPage` to consume the shared bar** — behavior must be unchanged (verify grades still render, single-child still hides the switcher, current-year still defaults). This is a pure refactor; do it in its own commit before adding attendance so any regression is isolated.

> If the refactor risks destabilizing the shipped grades page under time pressure, the fallback is to duplicate the two selectors into `ChildAttendancePage` and note the debt — but the extraction is preferred and low-risk.

### B3. Child Attendance page

**Route**: `/parent/attendance` · **Role**: Parent only.

```
frontend/src/pages/parent/attendance/ChildAttendancePage.tsx
```

Structure (mirrors `ChildGradesPage`):
1. **Bootstrap** — `getChildren()` + `getAcademicYears()` (React Query, `PARENT_KEYS`); render the shared `ParentChildYearBar`. Reuse the same empty/loading/error handling for the children list.
2. **Attendance query** — `parentPortalApi.getChildAttendance(childId, yearId)`, `enabled` once a child is selected; key `PARENT_KEYS.childAttendance`.
3. **Summary hero** — a small stat row / cards: **Attendance rate** (large, `attendanceRate == null ? '—' : \`${rate}%\``) + four counts (Present / Late / Absent / Excused) with the status colors. Include the `totalMarked` denominator as context ("of N marked days").
4. **Daily log table** — columns **Date**, **Status** (colored badge), **Section**, **Notes**. Sort `entries` by `date` **descending** in the component (don't rely on server order). Format `date` for display.
5. **Status badge colors** — **reuse the existing status→color map, do not invent new colors.** The identical `Present/Late/Absent/Excused` → Tailwind-class map already exists inline in [frontend/src/pages/admin/attendance/AttendanceViewPage.tsx](../frontend/src/pages/admin/attendance/AttendanceViewPage.tsx#L27-L30) and [frontend/src/pages/teacher/attendance/AttendancePage.tsx](../frontend/src/pages/teacher/attendance/AttendancePage.tsx#L38-L41) (green / amber / red / blue). This parent page would be the **third** copy — so extract the map (and a small `AttendanceStatusBadge`) into a shared module (e.g. `frontend/src/pages/attendance/statusColors.ts` or `components/AttendanceStatusBadge.tsx`) and have the summary counts and log rows consume it. Refactoring the two existing pages onto the shared badge is **optional** (keeps this slice small); if skipped, at minimum copy the map verbatim so colors stay consistent, and note the debt. The badge is **not yet catalogued** — add it to `.claude/catalog/frontend.md` when created.

**Empty / edge states (all required):**
- **No children linked** — shared bar's empty state ("No students are linked to your account yet. Contact the school office.").
- **Child selected, nothing marked for the year** — `totalMarked === 0`: friendly empty state ("No attendance has been recorded for {child} in {year} yet."); rate hero shows "—".
- **Loading / error** — spinner + retry-able error, consistent with `ChildGradesPage`.

### B4. Routes + nav

```tsx
// frontend/src/pages/parent/index.tsx — add the attendance route
<Route path="grades" element={<ChildGradesPage />} />
<Route path="attendance" element={<ChildAttendancePage />} />
<Route path="*" element={<Navigate to="grades" replace />} />
```

Add a **second** parent nav item to `AppShell` `NAV_ITEMS` (after "My Children"):

```tsx
{
  label: 'Attendance',
  to: '/parent/attendance',
  icon: <CalendarDays size={18} />,   // already imported in AppShell
  roles: ['Parent'],
},
```

(Optionally relabel the grades item "My Children" → "Grades" for parity now that there are two parent items — **ask first**; leaving it as "My Children" is acceptable.)

### B5. Project structure (frontend)

```
frontend/src/
  api/
    parentPortal.ts                         # modified — attendance types + client + key
  pages/parent/
    index.tsx                               # modified — add /attendance route
    ParentChildYearBar.tsx                  # new — shared switcher + year selector
    useParentChildYear.ts                   # new (recommended) — shared selection + defaulting hook
    grades/ChildGradesPage.tsx              # modified — consume shared bar (pure refactor)
    attendance/ChildAttendancePage.tsx      # new
  layouts/
    AppShell.tsx                            # modified — add "Attendance" nav item
```

---

## Implementation order

1. **Backend**: `StudentAttendanceSummaryDto` → `AttendanceService.GetStudentSummaryAsync` (+ its unit tests) → `ParentAttendanceDto` → `ParentPortalService.GetChildAttendanceAsync` (inject `AttendanceService`) → controller action → verify DI. Build + test.
2. **Frontend, commit 1 (refactor)**: extract `ParentChildYearBar` (+ `useParentChildYear`) and refactor `ChildGradesPage` onto it — no behavior change.
3. **Frontend, commit 2 (feature)**: `parentPortal.ts` attendance additions → `ChildAttendancePage` (hero → log → badges → empty states) → route + nav item.

Commit after backend-complete, after the refactor, and after the attendance page.

---

## Key invariants

- **Single authorization surface** — the attendance read passes through the existing `ResolveLinkedChildOrThrow`; no new auth code. Unlinked/unknown child → **404** (never 403, never leak existence).
- **Parent identity from JWT only** — `ParentUserId` is `User.FindFirstValue("sub")`, never a body/route field.
- **Reuse, don't fork, the history read** — `AttendanceService.GetStudentHistoryAsync` + `AttendanceHistoryEntryDto` are used unchanged; do not alter their ordering (shared with admin/teacher).
- **The rate formula is server-owned and singular** — `(Present + Late) / TotalMarked`, Absent + Excused count against, `null` at zero marked days. It lives in `AttendanceService`, computed once, never re-derived in the client.
- **All parent endpoints are GET/read-only** (SameSite=Lax CSRF rule); this feature never mutates — no `SaveChanges`, no transaction.
- **Tenant filter is never bypassed** — all reads respect the global `SchoolId` filter; no `IgnoreQueryFilters`.
- **No new entity, no migration** — a read composition (summary + existing history) over existing tables.
- **Children always listed** regardless of `EnrollmentStatus` (badged, not filtered), consistent with spec 18.

## Boundaries

- **Always:** derive the parent from the JWT; route the child read through the link guard; return 404 for an unlinked child; keep the endpoint GET/read-only; compute the rate only in `AttendanceService`; run `dotnet test SchoolMgmt.slnx` before done.
- **Ask first:** relabeling the existing "My Children" nav item; grouping the daily log by term/month instead of a flat descending list; excluding Excused from the rate denominator (this spec counts it against — the resolved formula); adding pagination to the log.
- **Never:** add Parent to `api/attendance` or reuse `GET /api/attendance/student-history` for parents (dedicated `api/parent` controller only); accept a trusted student/parent id from the request; add a write path; call `SaveChanges`/transactions; change `GetStudentHistoryAsync`'s ordering or signature; bypass the tenant query filter.

## Testing strategy

### Unit (`SchoolMgmt.Infrastructure.Tests` — hand-written fakes)

**`AttendanceService.GetStudentSummaryAsync`** (the rate rule):
- Mixed record set (e.g. 168 Present, 4 Late, 6 Absent, 2 Excused = 180) → counts correct; `AttendanceRate == Round((168+4)*100/180, 1) == 95.6`.
- **Excused counts against the rate** — a set with only Present + Excused yields rate < 100 (proves Excused is in the denominator but not the numerator).
- **Late counts as present** — Present + Late only → rate 100.
- **Zero marked days** → `TotalMarked == 0`, all counts 0, `AttendanceRate == null` (no divide-by-zero).

**`ParentPortalService.GetChildAttendanceAsync`** (the guard + orchestration):
- **Linked child** → returns `ParentAttendanceDto` with summary from `GetStudentSummaryAsync` and entries from `GetStudentHistoryAsync`; when `academicYearId` omitted, uses `GetCurrentAsync().Id`.
- **Unlinked child** (exists, not linked to caller) → `NotFoundException`.
- **Unknown child id** → `NotFoundException` (same 404, no existence leak).
- **No current year and no `academicYearId`** → `NotFoundException("No current academic year is set.")`.
- **Guard uses `GetLinkAsync(childId, parentUserId)`** (both args) — verified via the fake before any attendance read is issued.

### Integration (`SchoolMgmt.IntegrationTests` — real Postgres via Testcontainers)

Set-up: Parent-A linked to child A, unrelated Parent-B linked to child B; seed `AttendanceRecord` rows for A across a year (a mix of statuses).

- Parent-A `GET /api/parent/children/{A}/attendance` → 200; `summary` counts/rate match the seeded mix; `entries` returns A's rows; omitting `academicYearId` returns the current year.
- **IDOR:** Parent-A `GET /api/parent/children/{B}/attendance` → **404**.
- Parent-A `GET /api/parent/children/{unknownGuid}/attendance` → 404.
- **Auth matrix:** the endpoint → **401** unauthenticated; → **403** for Admin or Teacher (Parent-only).
- A child with **no** attendance rows → 200 with `summary.totalMarked == 0`, `attendanceRate == null`, `entries == []`.

## Success criteria

- `AttendanceService.GetStudentSummaryAsync` returns correct per-status counts and `rate = (Present+Late)/TotalMarked` (Absent + Excused against; `null` at zero), tenant-scoped.
- `GET /api/parent/children/{childId}/attendance` returns the combined summary + daily log for a linked child (current year by default), and **404s** for any child not linked to the caller.
- The endpoint is GET, returns 401 unauthenticated and 403 for non-Parent roles.
- Frontend: second "Attendance" nav item → `/parent/attendance`; shared `ParentChildYearBar` drives both parent pages; grades page behavior unchanged after the refactor; attendance page shows the rate hero + four counts over a descending daily log with colored status badges; real empty states for no-children and zero-marked.
- The rate is never computed in the client — the UI renders `summary.attendanceRate` verbatim.
- All unit + integration tests above pass under `dotnet test SchoolMgmt.slnx`; frontend builds clean.

## Resolved decisions (from idea doc 16)

- **Attendance-rate formula — `(Present + Late) ÷ TotalMarked`**, Absent + Excused count against, all four counts shown, `null` at zero marked days. Server-owned in `AttendanceService`.
- **View depth — summary + log** (rate hero over the daily list), not summary-only or raw-log-only.
- **Summary computation — server-side** (business rule, reusable by the dashboard), not client-derived.
- **Navigation — two nav items** ("My Children"/Grades + "Attendance"), each its own page sharing the extracted `ParentChildYearBar`; not a tabbed page and not a parent-home landing (both deferred).
- **Log ordering — reverse-chronological, flat** (no term/month grouping); the summary carries the at-a-glance signal.
