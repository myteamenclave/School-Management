# Parent Portal — View Child's Grades

## Problem Statement
**How Might We** let a logged-in parent open the portal and see their own child's grades — and *only* their own child's — as a clean, report-card-style read, without ever trusting a client-supplied student ID?

## Context

This is the **first parent-facing read surface** in the system. It sits directly on top of two prior features:

- **Parent–guardian link ([14-parent-guardian-link.md](14-parent-guardian-link.md) / spec 17):** the `StudentParent` junction resolves *parent user → their children*. This is the authorization anchor. Spec 17 explicitly deferred "a logged-in parent viewing their child's grades" to "a future spec" — this is that spec.
- **Grade entry ([12-grade-entry.md](12-grade-entry.md) / spec 15):** `GradebookService.GetStudentGradesAsync(studentId, academicYearId)` and `StudentGradeDto` already exist, exposed **Admin/Teacher only** via `GET /api/grades/student?studentId=`. Spec 15 notes this is "the read surface the parent portal will later reuse." The gradebook already computes Term score + Letter grade server-side; there is no GPA/cross-subject rollup anywhere.

What does **not** exist yet: any parent-facing endpoint, and any parent UI. A Parent logs in today and lands on a bare `Welcome, {name}` page; `ParentRoutes` is an empty `<Outlet/>` and the sidebar shows them only a "Dashboard" link. So this slice also establishes two patterns the next three parent features (attendance, fees, pay) will copy: **how a parent is authorized to a child**, and **how a parent picks which child** they're viewing.

## Recommended Direction
Build the grades read as a dedicated, JWT-scoped slice. A new `api/parent/*` controller resolves the caller's children from their `StudentParent` links (never from a request parameter): one endpoint lists the parent's children (feeding a child switcher), another returns a chosen child's grades for a chosen academic year. Every child-scoped call passes through a single `ResolveLinkedStudentOrThrow(parentUserId, childId)` guard, so authorization is one code path, not a per-endpoint checkbox.

On the frontend, this slice also lays the **reusable parent portal shell** the next three parent features will slot into: a parent landing with a child switcher (dropdown; auto-selected when there's one child) and an academic-year selector defaulting to the current year. The grades view itself is report-card style — subject, Term score, Letter grade, grouped by semester — reusing the existing server-computed `Term`/`Letter` and `StudentGradeDto`. No raw component scores, no GPA, no new grade math.

Backend reuse is high (the `GetByStudentAndYear` repo method and DTO already exist); the genuinely new work is the parent authorization pattern and the parent shell — both of which are the point, since attendance/fees/pay will copy them.

## Key Assumptions to Validate
- [ ] **Parents have few children (1–3 typical).** The switcher UX assumes a short list. Confirm against the seed/demo data model.
- [ ] **The existing `StudentGradeDto` is sufficient** for a parent report-card view (Term + Letter + semester grouping) with no new fields. Mock the grades table against the DTO before touching backend.
- [ ] **`ResolveLinkedStudentOrThrow` is the only authorization surface needed** — no per-year or per-subject sub-permissions. Walk each planned parent endpoint (grades now; attendance/fees later) and confirm they all reduce to "is this child linked to me?"
- [ ] **A parent sees grades regardless of the child's `EnrollmentStatus`** (a withdrawn child's prior grades stay visible). Confirm the intended rule — see Open Questions.

## MVP Scope
**In:**
- `GET /api/parent/children` → the caller's linked children (id, name, code, current grade/section label), derived from JWT.
- `GET /api/parent/children/{childId}/grades?academicYearId=` → that child's grades, guarded by the link check; defaults to current year when omitted.
- Parent portal shell: landing route under `ParentRoutes`, child switcher, academic-year selector (reuse existing academic-years list).
- Grades view: report-card table (subject · Term · Letter), grouped by semester, with real empty states (no children, no grades, child not yet enrolled).
- Parent nav item in the sidebar; `RequireRole("Parent")` guard.

**Out:**
- Full component breakdown (Midterm/Final/Coursework) — Term + Letter only.
- Parent overview dashboard with cross-module summaries (needs attendance/fees data that isn't built).
- Any write/edit capability for parents.

## Not Doing (and Why)
- **Reusing the admin `/api/grades/student?studentId=` endpoint** — mixing admin and parent auth is one missed check from an IDOR leak; a dedicated JWT-scoped controller is safer and clearer.
- **GPA / class rank / honor roll** — spec 15 deliberately has no cross-subject aggregation; there's nothing to expose and inventing it here is scope creep.
- **Report-card PDF export / print view** — a fast-follow if asked; not needed to prove the read surface.
- **New-grade notifications (email/SMS/in-app)** — SMS is explicitly out of project scope; notifications are a separate concern.
- **Component-score visibility** — parents read final grades, not in-progress component marks; keeps the view calm and avoids questions about un-weighted raw scores.

## Resolved Decisions
- **Withdrawn/transferred children:** show **all** linked children regardless of `EnrollmentStatus`, badged. A report card is historical; the `StudentParent` link is the authorization truth; access revocation already has an explicit path (spec 17 link `DELETE`), so status must not double as a hide mechanism.
- **Zero-grade children / empty years:** always show the child and the year; render a no-grades empty state rather than hiding real entities.
- **Post-login landing:** parent lands on `/parent/grades`; a neutral parent home is deferred until a second parent module (attendance/fees) exists.
