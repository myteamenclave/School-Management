# Spec 18 — Implement Parent Portal: View Child's Grades (Backend + Frontend)

## Related docs & prior specs

- **Idea doc**: [docs/ideas/15-parent-portal-grades.md](../docs/ideas/15-parent-portal-grades.md) — problem statement, JWT-scoped authorization direction, "grades + reusable shell" slice decision, report-card (Term+Letter) data-depth decision, open questions.
- **Parent–guardian link** ([specs/17-implement-parent-guardian-link.md](17-implement-parent-guardian-link.md)) — provides the `StudentParent` junction (parent `User` ↔ `Student`) that is this feature's **authorization anchor**, `UserRole.Parent`, and `IStudentParentRepository` (with `GetLinkAsync`, extended here). Spec 17 explicitly deferred "a logged-in parent viewing their child's grades" to a future spec — **this is that spec**, and the **first parent-facing read endpoint** in the system.
- **Grade entry** ([specs/15-implement-grade-entry.md](15-implement-grade-entry.md)) — provides `GradebookService.GetStudentGradesAsync(studentId, academicYearId)`, `StudentGradeDto`, and `ISubjectTermGradeRepository.GetByStudentAndYearAsync` (Subject + Semester included). This slice **reuses that read path unchanged**; the server-computed `TermScore`/`LetterGrade` are the single source of truth. Note the marks controller is `api/gradebook` (Admin/Teacher) — this spec adds a **separate** `api/parent` controller, it does **not** widen `api/gradebook`.
- **Class/section assignment** ([specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md)) — `StudentSectionEnrollment` + `IStudentSectionEnrollmentRepository.GetByStudentIdAsync` (Section→Grade + AcademicYear included), used to label a child's current grade/section.
- **Academic year / term** ([specs/03-implement-academic-year-term-configuration.md](03-implement-academic-year-term-configuration.md)) — `IAcademicYearRepository.GetCurrentAsync()`, used to default the year when the request omits `academicYearId`.
- **Auth** ([specs/02-implement-auth.md](02-implement-auth.md)) — JWT-cookie auth, `[Authorize(Roles = "Parent")]`, `User.FindFirstValue("sub")` for the caller's user id.
- **Frontend scaffold** ([specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md)) — `RoleRoute`, `ParentRoutes`, `AppShell` nav, Zustand auth store (`role: 'Parent'`), Axios client.
- **Rules**: [.claude/rules/backend.md](../.claude/rules/backend.md) — thin controllers, one service per feature area, repositories touch only the `DbSet`, `IUnitOfWork` owns persistence, **GET must be read-only**, DI conventions, interfaces-where-consumed.

## Overview

A logged-in **Parent** opens the portal, sees a list of **their own** linked children, picks one child and (optionally) an academic year, and views that child's grades as a **report card**: subject · Term score · Letter grade, grouped by semester. Everything is **read-only**.

The load-bearing rule is authorization: a parent may only ever see a child linked to them via `StudentParent`. **No endpoint accepts a raw student id it hasn't verified against the caller's links.** A single guard, `ResolveLinkedChildOrThrow(parentUserId, childId)`, is the only path to a child; an unlinked (or non-existent) child returns **404** (never reveal existence). This slice also lays the reusable parent portal shell — child switcher + year selector — that the queued attendance/fees/pay features will copy.

**Scope decisions (from idea doc 15):**
- **In:** `GET /api/parent/children`, `GET /api/parent/children/{childId}/grades`, `GET /api/parent/academic-years`; parent portal shell (nav item, child switcher, year selector, report-card grades view with empty states).
- **Out:** component-score breakdown (Term + Letter only), parent overview dashboard, any write path, PDF export, notifications, GPA/rank.

---

## Part A — Backend

No new entities, no migration. New Application feature folder `ParentPortal/`, one new controller, one repository-method addition.

### A1. `IStudentParentRepository` — one addition

Spec 17 gave us the student→parents direction (`GetByStudentIdAsync`) and the link check (`GetLinkAsync`). This feature needs the **parent→children** direction:

```csharp
// Application/ParentAccounts/IStudentParentRepository.cs  (add one method)
public interface IStudentParentRepository : IRepository<StudentParent>
{
    Task<StudentParent?> GetLinkAsync(Guid studentId, Guid userId, CancellationToken ct = default);   // existing
    Task<List<StudentParent>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default);     // existing
    Task<List<StudentParent>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);           // NEW — Include Student
}
```

Implementation (Infrastructure `StudentParentRepository`), includes `Student` and orders for a stable switcher:

```csharp
public Task<List<StudentParent>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
    DbSet
        .Include(x => x.Student)
        .Where(x => x.UserId == userId)
        .OrderBy(x => x.Student.LastName)
        .ThenBy(x => x.Student.FirstName)
        .ToListAsync(ct);
```

The global `SchoolId` query filter applies automatically — no manual tenant clause. `GetLinkAsync` is reused as-is for the per-child authorization guard (it already filters by both `studentId` and `userId`, and is tenant-scoped).

### A2. DTOs

```csharp
// Application/ParentPortal/Dtos/ParentChildDto.cs
// One linked child in the switcher. CurrentGradeLabel/CurrentSectionName are null
// when the child has no enrollment in the CURRENT academic year (e.g. not yet enrolled
// this year, or withdrawn) — the switcher still lists them.
public record ParentChildDto(
    Guid StudentId,
    string StudentName,          // "First Last"
    string StudentCode,
    string EnrollmentStatus,     // Student.EnrollmentStatus.ToString() — UI may badge withdrawn/etc.
    string? CurrentGradeLabel,   // e.g. "Grade 5"   (from current-year enrollment)
    string? CurrentSectionName   // e.g. "A"
);

// Minimal year list for the parent year-selector. NOT the admin AcademicYearDto
// (no semesters, no status internals) — parent-scoped, read-only.
// Application/ParentPortal/Dtos/ParentAcademicYearDto.cs
public record ParentAcademicYearDto(Guid Id, string Name, bool IsCurrent);
```

The grades payload reuses **`StudentGradeDto`** from spec 15 unchanged (id, subjectId, subjectName, semesterId, semesterName, midterm/final/coursework, termScore, letterGrade, notes). The frontend renders only subject · term · letter · semester; the extra fields are ignored, keeping the DTO shared with the admin/teacher student-grade view.

### A3. `ParentPortalService`

One service, direct DI, one method per use case. This is the **only** place the link guard lives.

```csharp
// Application/ParentPortal/ParentPortalService.cs
public class ParentPortalService(
    IStudentParentRepository links,
    IStudentSectionEnrollmentRepository enrollments,
    IAcademicYearRepository years,
    GradebookService gradebook)                 // reuse the existing grade read path
{
    // GET children — the caller's linked children, labelled with their current-year grade/section.
    public async Task<List<ParentChildDto>> GetMyChildrenAsync(Guid parentUserId, CancellationToken ct = default)
    {
        var childLinks = await links.GetByUserIdAsync(parentUserId, ct);
        var currentYear = await years.GetCurrentAsync(ct);

        var result = new List<ParentChildDto>(childLinks.Count);
        foreach (var link in childLinks)
        {
            var s = link.Student;
            string? gradeLabel = null, sectionName = null;
            if (currentYear is not null)
            {
                // GetByStudentIdAsync includes Section→Grade + AcademicYear; pick the current-year row.
                var enr = (await enrollments.GetByStudentIdAsync(s.Id, ct))
                    .FirstOrDefault(e => e.AcademicYearId == currentYear.Id);
                if (enr is not null)
                {
                    gradeLabel = enr.Section.Grade.Name;   // Grade.Name e.g. "Grade 5"
                    sectionName = enr.Section.Name;        // Section.Name e.g. "A"
                }
            }
            result.Add(new ParentChildDto(
                s.Id, $"{s.FirstName} {s.LastName}", s.StudentCode,
                s.EnrollmentStatus.ToString(), gradeLabel, sectionName));
        }
        return result;
    }

    // GET grades for one linked child. academicYearId null => current year.
    public async Task<List<StudentGradeDto>> GetChildGradesAsync(
        Guid parentUserId, Guid childId, Guid? academicYearId, CancellationToken ct = default)
    {
        await ResolveLinkedChildOrThrow(parentUserId, childId, ct);   // AUTHORIZATION — every child read goes through here

        var yearId = academicYearId
            ?? (await years.GetCurrentAsync(ct))?.Id
            ?? throw new NotFoundException("No current academic year is set.");

        return await gradebook.GetStudentGradesAsync(childId, yearId, ct);
    }

    // Minimal year list for the selector.
    public async Task<List<ParentAcademicYearDto>> GetAcademicYearsAsync(CancellationToken ct = default) =>
        (await years.GetAllWithSemestersAsync(ct))    // existing list method; semesters ignored here
            .OrderByDescending(y => y.StartDate)
            .Select(y => new ParentAcademicYearDto(y.Id, y.Name, y.IsCurrent))
            .ToList();

    // The single authorization surface. Unlinked OR non-existent child => 404 (do not leak existence).
    private async Task ResolveLinkedChildOrThrow(Guid parentUserId, Guid childId, CancellationToken ct)
    {
        var link = await links.GetLinkAsync(childId, parentUserId, ct);
        if (link is null)
            throw new NotFoundException("Child not found.");
    }
}
```

Notes:
- **Why 404, not 403:** there is no `ForbiddenException` in the codebase and, more importantly, a linked-or-not check that returned 403 would confirm the child id exists. `NotFoundException` (mapped to 404 by `DomainExceptionFilter`) is both the existing pattern and the correct security posture.
- `parentUserId` **always** comes from the JWT `sub` claim in the controller — never from the request body/route as a trusted identity.
- `GetChildGradesAsync` does **not** re-filter grades by parent — the guard already proved the child is linked, and `GetStudentGradesAsync` is keyed by `studentId` within the tenant filter.
- Resolved against current code: list method is `IAcademicYearRepository.GetAllWithSemestersAsync`; labels are `Grade.Name` / `Section.Name`.

### A4. `ParentPortalController`

Thin controller, Parent-only, all GET (read-only per the CSRF rule). The caller identity is the JWT `sub`; child id is a route param that the service validates against the caller's links.

```csharp
// WebApi/Controllers/ParentPortalController.cs
using System.Security.Claims;

[ApiController]
[Route("api/parent")]
[Authorize(Roles = "Parent")]
public class ParentPortalController(ParentPortalService service) : ControllerBase
{
    private Guid ParentUserId => Guid.Parse(User.FindFirstValue("sub")!);

    // GET /api/parent/children
    [HttpGet("children")]
    public async Task<IActionResult> GetChildren(CancellationToken ct)
        => Ok(await service.GetMyChildrenAsync(ParentUserId, ct));

    // GET /api/parent/children/{childId}/grades?academicYearId=
    [HttpGet("children/{childId:guid}/grades")]
    public async Task<IActionResult> GetChildGrades(
        Guid childId, [FromQuery] Guid? academicYearId, CancellationToken ct)
        => Ok(await service.GetChildGradesAsync(ParentUserId, childId, academicYearId, ct));

    // GET /api/parent/academic-years
    [HttpGet("academic-years")]
    public async Task<IActionResult> GetAcademicYears(CancellationToken ct)
        => Ok(await service.GetAcademicYearsAsync(ct));
}
```

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `GET` | `/api/parent/children` | `GetMyChildrenAsync` | 200 — `List<ParentChildDto>` | Empty list if the parent has no links |
| `GET` | `/api/parent/children/{childId}/grades` | `GetChildGradesAsync` | 200 — `List<StudentGradeDto>` | **404** if child not linked to caller; `academicYearId` optional (defaults to current) |
| `GET` | `/api/parent/academic-years` | `GetAcademicYearsAsync` | 200 — `List<ParentAcademicYearDto>` | Read-only year list for the selector |

No `DomainExceptionFilter` change — `NotFoundException`→404 already mapped.

### A5. DI registration

`Application/DependencyInjection.cs`:
```csharp
services.AddScoped<ParentPortalService>();
```
(`GradebookService` is already registered by spec 15; `ParentPortalService` depends on it.)

No Infrastructure DI change — `IStudentParentRepository`, `IStudentSectionEnrollmentRepository`, `IAcademicYearRepository` are all already registered; only the new interface **method** is added to the existing implementation.

### A6. Project structure (backend)

```
backend/
  SchoolMgmt.Application/
    ParentAccounts/
      IStudentParentRepository.cs           # modified — add GetByUserIdAsync
    ParentPortal/                           # new folder
      ParentPortalService.cs                # new
      Dtos/
        ParentChildDto.cs                   # new
        ParentAcademicYearDto.cs            # new
    DependencyInjection.cs                  # add ParentPortalService

  SchoolMgmt.Infrastructure/
    Persistence/Repositories/
      StudentParentRepository.cs            # modified — implement GetByUserIdAsync

  SchoolMgmt.WebApi/
    Controllers/
      ParentPortalController.cs             # new
```

---

## Part B — Frontend

### B1. API client

```ts
// frontend/src/api/parentPortal.ts
import api from './axios'

export interface ParentChild {
  studentId: string
  studentName: string
  studentCode: string
  enrollmentStatus: string
  currentGradeLabel: string | null
  currentSectionName: string | null
}

export interface ParentAcademicYear {
  id: string
  name: string
  isCurrent: boolean
}

// Reuse the same shape the admin/teacher student-grade view uses (spec 15 StudentGrade).
export interface StudentGrade {
  id: string
  subjectId: string; subjectName: string
  semesterId: string; semesterName: string
  midtermScore: number | null
  finalScore: number | null
  courseworkScore: number | null
  termScore: number | null
  letterGrade: string | null
  notes: string | null
}

export const PARENT_KEYS = {
  children: () => ['parent', 'children'] as const,
  academicYears: () => ['parent', 'academic-years'] as const,
  childGrades: (childId: string, academicYearId: string) =>
    ['parent', 'grades', childId, academicYearId] as const,
}

export const parentPortalApi = {
  getChildren: () =>
    api.get<ParentChild[]>('/parent/children').then((r) => r.data),
  getAcademicYears: () =>
    api.get<ParentAcademicYear[]>('/parent/academic-years').then((r) => r.data),
  getChildGrades: (childId: string, academicYearId?: string) =>
    api.get<StudentGrade[]>(`/parent/children/${childId}/grades`, {
      params: academicYearId ? { academicYearId } : undefined,
    }).then((r) => r.data),
}
```

### B2. Parent portal routes + shell

`ParentRoutes` is currently a bare `<Outlet/>`. Convert it to nested routes (mirroring `TeacherRoutes`), with a redirect from the parent root to the grades page:

```tsx
// frontend/src/pages/parent/index.tsx
import { Routes, Route, Navigate } from 'react-router-dom'
import { ChildGradesPage } from './grades/ChildGradesPage'

export function ParentRoutes() {
  return (
    <Routes>
      <Route path="grades" element={<ChildGradesPage />} />
      <Route path="*" element={<Navigate to="grades" replace />} />
    </Routes>
  )
}
```

Add the Parent nav item to `AppShell` `NAV_ITEMS` (roles `['Parent']`), e.g. label **"My Children"**, `to: '/parent/grades'`, `GraduationCap` icon (already imported).

> The existing `DashboardPage` keeps its bare `Welcome, {name}` for Parent — do **not** repurpose `/dashboard` here. The parent's real surface is `/parent/grades`. (A parent dashboard landing is explicitly out of scope; the "Dashboard" nav link remains but is a thin welcome until a later slice.)

### B3. Child Grades page

**Route**: `/parent/grades` · **Role**: Parent only (guarded by `RoleRoute role="Parent"`).

```
frontend/src/pages/parent/grades/ChildGradesPage.tsx
```

Structure:
1. **Bootstrap queries** — `parentPortalApi.getChildren()` and `parentPortalApi.getAcademicYears()` (React Query, keys from `PARENT_KEYS`).
2. **Child switcher** — a `Select` of the parent's children (label: `studentName` + `studentCode`, and `currentGradeLabel`/`currentSectionName` when present). **Auto-select** when there is exactly one child; hide the switcher in that single-child case (show the child's name as a heading instead). Badge non-`Active` `enrollmentStatus`.
3. **Year selector** — a `Select` of `ParentAcademicYear`, defaulting to the one with `isCurrent === true` (fallback: first in the list). Reuses the same visual pattern as the dashboard `YearSelector`.
4. **Grades query** — `parentPortalApi.getChildGrades(childId, yearId)`, enabled once a child is selected.
5. **Report-card table** — grades grouped by `semesterName`; columns: **Subject**, **Term** (`termScore`, one decimal or "—"), **Letter** (badge, or "—"). Do **not** render midterm/final/coursework. A subject with `termScore === null` shows "—" for both Term and Letter (grade in progress).
6. **Letter badge** — reuse the letter badge component from spec 15 (B7) if present; otherwise a neutral badge.

**Empty / edge states (all required):**
- **No children linked** — friendly empty state ("No students are linked to your account yet. Contact the school office.").
- **Child selected, no grades for the year** — "No grades have been published for {child} in {year} yet."
- **Child not enrolled in the selected year** — same no-grades empty state (the grades query simply returns `[]`).
- **Loading / error** — spinner and a retry-able error message (consistent with existing pages).

### B4. Project structure (frontend)

```
frontend/src/
  api/
    parentPortal.ts                         # new
  pages/parent/
    index.tsx                               # modified — nested Routes + redirect
    grades/
      ChildGradesPage.tsx                   # new
  layouts/
    AppShell.tsx                            # modified — add Parent nav item
```

---

## Implementation order

1. **Backend**: add `GetByUserIdAsync` to `IStudentParentRepository` + impl → DTOs → `ParentPortalService` (guard first) → `ParentPortalController` → DI. Build.
2. **Frontend**: `parentPortal.ts` → `ParentRoutes` nested routes + AppShell nav → `ChildGradesPage` (switcher → year selector → report-card table → empty states).

Commit after backend-complete and after the frontend page.

---

## Key invariants

- **Single authorization surface** — every child-scoped read passes through `ResolveLinkedChildOrThrow`; no endpoint trusts a client-supplied student id. Unlinked/unknown child → **404** (never 403, never leak existence).
- **Parent identity from JWT only** — `parentUserId` is `User.FindFirstValue("sub")`, never a body/route field.
- **Reuse, don't fork, the grade read** — `GradebookService.GetStudentGradesAsync` + `StudentGradeDto` are used unchanged; server-computed `TermScore`/`LetterGrade` remain the source of truth. No new grade math, no GPA/rank.
- **All parent endpoints are GET and read-only** (`SameSite=Lax` CSRF rule). No parent write path exists in this slice.
- **Tenant filter is never bypassed** — all repository reads respect the global `SchoolId` filter; no `IgnoreQueryFilters`.
- **No new entity, no migration** — this is a read composition over existing tables.
- **Report-card depth** — the parent UI shows Subject · Term · Letter · Semester only; component scores are fetched-but-not-rendered (DTO shared) and never surfaced.
- **Children always listed** regardless of `EnrollmentStatus` or whether grades exist (a parent can see a withdrawn/past child's report card) — status is badged, not filtered.

## Boundaries

- **Always:** derive the parent from the JWT; route every child read through the link guard; return 404 for an unlinked child; keep all endpoints GET/read-only; run `dotnet test SchoolMgmt.slnx` before done.
- **Ask first:** filtering the children list by `EnrollmentStatus` (this spec lists all — see open question); exposing component scores to parents; adding a parent overview/dashboard landing; letting the year selector show only years the child has grades in (this spec lists all years).
- **Never:** add Parent to the `api/gradebook` controller or reuse `GET /api/gradebook/student?studentId=` for parents (dedicated `api/parent` controller only); accept a trusted student/parent id from the request body; add a write path; call `SaveChanges`/transactions (this feature never mutates); bypass the tenant query filter.

## Testing strategy

### Unit (`SchoolMgmt.Infrastructure.Tests` — hand-written fakes)

`ParentPortalService` carries the guard, so it gets focused unit tests with fake repositories and a fake/real `GradebookService` collaborator:

- **Children — happy path:** parent linked to two students → both returned, ordered by last/first name; `CurrentGradeLabel`/`CurrentSectionName` populated from the current-year enrollment; a child with no current-year enrollment returns null labels but is still listed.
- **Children — no links:** parent with no `StudentParent` rows → empty list (not an error).
- **Grades — linked child:** returns `GetStudentGradesAsync(childId, yearId)`; when `academicYearId` omitted, uses `GetCurrentAsync().Id`.
- **Grades — unlinked child:** child exists but not linked to caller → `NotFoundException`.
- **Grades — unknown child id:** → `NotFoundException` (same 404, no existence leak).
- **Grades — no current year and no `academicYearId`:** → `NotFoundException("No current academic year is set.")`.
- **Guard uses `GetLinkAsync(childId, parentUserId)`** (both args), verified via the fake — proves the check is per-(child,parent), not just per-child.

### Integration (`SchoolMgmt.IntegrationTests` — real Postgres via Testcontainers)

Set-up: seed a Parent linked to child A, a second unrelated Parent linked to child B, and some `SubjectTermGrade` rows for A.

- Parent-A `GET /api/parent/children` → 200, lists child A only (not B).
- Parent-A `GET /api/parent/children/{A}/grades` → 200 with A's grades; omitting `academicYearId` returns the current year's grades.
- **IDOR:** Parent-A `GET /api/parent/children/{B}/grades` → **404** (B belongs to another parent).
- Parent-A `GET /api/parent/children/{unknownGuid}/grades` → 404.
- `GET /api/parent/academic-years` → 200 with `isCurrent` flagged.
- **Auth matrix:** every `api/parent/*` endpoint → **401** unauthenticated; → **403** for an Admin or Teacher role (Parent-only).
- A parent with a child that has **no** grades → `children/{id}/grades` returns `200 []`.

## Success criteria

- `IStudentParentRepository.GetByUserIdAsync` returns a parent's linked children (Student included), tenant-scoped.
- `GET /api/parent/children` lists exactly the caller's children with current-year grade/section labels (null when unenrolled); no other family's children appear.
- `GET /api/parent/children/{childId}/grades` returns that child's grades (current year by default, or the requested year) and **404s** for any child not linked to the caller.
- `GET /api/parent/academic-years` returns the minimal year list with `isCurrent`.
- All `api/parent/*` endpoints are GET, return 401 unauthenticated and 403 for non-Parent roles.
- The parent frontend: nav item → `/parent/grades`; child switcher (auto-selected/hidden for a single child); current-year-default year selector; report-card table (Subject · Term · Letter, grouped by semester); real empty states for no-children / no-grades.
- Component scores are never rendered in the parent UI.
- All unit and integration tests above pass under `dotnet test SchoolMgmt.slnx`; frontend builds clean.

## Resolved decisions (from idea doc 15 open questions)

- **Withdrawn/transferred children — list ALL linked children** regardless of `EnrollmentStatus` (badged, not filtered). A report card is a historical record; the `StudentParent` link is the authorization truth, not enrollment status; and revoking a parent's access has an explicit path (spec 17's link `DELETE`), so status must not double as a hide mechanism.
- **Year selector — list ALL school years,** not only years the child has grades in. Simpler and consistent with the admin/teacher selectors; an empty past year just shows the no-grades empty state.
- **Post-login landing — parent lands on `/parent/grades`** via nav; the `/dashboard` welcome stays a stub. A dedicated parent home is deferred to when a second parent module (attendance/fees) exists.
```
