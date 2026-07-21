# Parent Portal — View Child's Attendance

## Problem Statement
**How Might We** let a logged-in parent see their own child's daily attendance for a chosen year — reusing the existing parent-portal auth and shell — in a form that answers *"is my kid actually showing up?"* at a glance, rather than dumping a raw 180-row roll-call log?

## Context

This is the **second parent-facing read surface**, and it deliberately copies the pattern spec 18 established for grades:

- **Parent portal shell + auth ([15-parent-portal-grades.md](15-parent-portal-grades.md) / spec 18):** the `api/parent` controller, the `ParentPortalService`, the single `ResolveLinkedChildOrThrow(parentUserId, childId)` authorization guard (unlinked/unknown child → 404, never trust a client-supplied student id), `GET /api/parent/children`, `GET /api/parent/academic-years`, the child switcher and year selector already exist. This feature slots a third child-scoped read into that exact frame.
- **Attendance marking ([11-attendance-marking.md](11-attendance-marking.md) / spec 14):** `AttendanceService.GetStudentHistoryAsync(studentId, academicYearId)` already returns a student's full year of records as `AttendanceHistoryEntryDto (id, sectionId, sectionName, date, status, notes)`. Spec 11 explicitly built this history read *"for parent portal and dashboard."* This is the parent-portal half of that promise.

**What makes this NOT a pure copy of the grades slice:** grades arrived pre-summarized — the server computes Term score + Letter grade, so the parent view just renders them. **Attendance has no server-side rollup today.** A year is a flat list of daily statuses; the *signal a parent wants is the rate and the recent absences*, not 180 undifferentiated rows. So this slice adds the one thing grades didn't need: a **server-computed attendance summary**.

## Recommended Direction

Add a single child-scoped endpoint, `GET /api/parent/children/{childId}/attendance?academicYearId=`, to the existing `ParentPortalController`. It routes through the **same** `ResolveLinkedChildOrThrow` guard (no new auth surface), then returns a **combined payload**: a summary block (per-status counts + an attendance rate) *and* the reverse-chronological daily log. The frontend renders the summary as the hero ("94% attendance — 168 present, 4 late, 6 absent, 2 excused") and the log beneath it (date · status badge · notes), color-coded by status.

The **summary is computed server-side** — not because the client can't count, but because the *attendance-rate formula is a business rule* (does "Excused" count as attended? is "Late" present?) that must have one authoritative definition, reusable by the admin dashboard later. It lives in `AttendanceService` (a new `GetStudentSummaryAsync`), keeping the rate rule owned by the attendance feature; `ParentPortalService` just orchestrates guard → summary → log into one DTO. `AttendanceHistoryEntryDto` is reused **unchanged** for the log rows.

Navigation: a **second parent nav item, "Attendance"** (`/parent/attendance`), alongside the existing grades page — the smallest additive change, consistent with how Teacher/Admin nav is structured. To avoid copy-pasting the child switcher + year selector into a second page, extract them from spec-18's `ChildGradesPage` into a shared `ParentChildYearBar` (or a small hook) that both parent pages consume. This is DRY without the larger "one tabbed page" or "parent home landing" redesign, both of which are deliberately deferred.

## Key Assumptions to Validate
- [ ] **The existing `GetStudentHistoryAsync` + `AttendanceHistoryEntryDto` are sufficient for the log** with no new fields (date, section, status, notes cover the report). Mock the log table against the DTO before touching backend.
- [ ] **A year of attendance (~180 rows) is fine to return in one payload** for both the summary and the log — no pagination needed at demo scale. Confirm against seed data volume.
- [ ] **The attendance rate is the number parents care about**, and one formula satisfies them (see Open Questions for the exact denominator). Validate the default framing with the client before wiring the summary.
- [ ] **A parent sees attendance regardless of the child's `EnrollmentStatus`** (a withdrawn child's prior record stays visible) — same rule spec 18 resolved for grades; the `StudentParent` link is the authorization truth, not enrollment status.
- [ ] **No new authorization sub-permissions are needed** — the attendance read reduces entirely to "is this child linked to me?", so it reuses `ResolveLinkedChildOrThrow` untouched.

## MVP Scope

**In:**
- **Backend:** `AttendanceService.GetStudentSummaryAsync(studentId, academicYearId)` → per-status counts + rate; `ParentPortalService.GetChildAttendanceAsync(parentUserId, childId, academicYearId?)` (guard → summary + history) returning a combined `ParentAttendanceDto { summary, entries }`; `GET /api/parent/children/{childId}/attendance?academicYearId=` on the existing controller (Parent-only, GET/read-only, defaults to current year when omitted).
- **Frontend:** second parent nav item "Attendance"; `ChildAttendancePage` — summary hero (rate + four counts) over a reverse-chronological daily log (date · status badge · notes), status-colored badges (Present green / Late amber / Absent red / Excused blue).
- **Refactor:** extract child switcher + year selector into a shared `ParentChildYearBar`/hook used by both grades and attendance pages.
- **Empty/edge states:** no children linked; child selected but no attendance marked for the year; loading/error — mirroring spec 18.

**Out:**
- Per-subject / period-level attendance (system records daily roll call only — spec 11 decision).
- Any write/edit path for parents (corrections go through the teacher, per spec 11).
- Absence notifications / alerts (SMS explicitly out of project scope).
- Attendance trends/charts over time (belongs to the dashboard module, not a parent read slice).
- A combined tabbed child page or a parent-home landing (deferred; two nav items for now).

## Not Doing (and Why)
- **Computing the rate client-side** — the formula is a business rule; a `.filter()` in React is one refactor away from the parent view and the future dashboard disagreeing on what "attendance rate" means. One server-side definition instead.
- **A new attendance DTO for the log** — `AttendanceHistoryEntryDto` already carries date/section/status/notes; forking it adds surface for nothing. Only the *summary* is new.
- **Widening `api/attendance` for parents** — same IDOR risk spec 18 avoided for grades; the dedicated `api/parent` controller with its link guard stays the only parent path.
- **Pagination / infinite scroll on the log** — a single academic year is bounded (~180 rows); pagination is premature complexity at this scale.
- **Refactoring the grades page into tabs, or building a parent home** — real UX improvements, but they turn an additive read slice into a shell redesign. Deferred until there's a third parent module or an explicit ask.

## Open Questions
- **Should the log default to whole-year, or most-recent-first with the current term expanded?** Recommended: reverse-chronological full year, no grouping — simplest, and the summary already carries the at-a-glance signal.

## Resolved Decisions
- **Attendance-rate formula:** **rate = (Present + Late) ÷ Total Marked Days.** "Physically in class" (Present or Late) over all marked days; **Absent AND Excused both count against the rate**, but all four raw counts are always shown so an excused absence reads as excused, not truancy. One authoritative server-side definition, reusable by the future dashboard. Zero marked days → rate is `null` (rendered as "—", no attendance yet), never a divide-by-zero.

## Resolved (inherited from spec 18, restated for this slice)
- **Withdrawn/transferred children:** listed and viewable regardless of `EnrollmentStatus` (badged, not filtered) — the link is the authorization truth; revocation has an explicit path (spec 17 link `DELETE`).
- **Unlinked/unknown child:** `ResolveLinkedChildOrThrow` → **404**, never leak existence, never 403.
- **Year selector:** lists all school years; an empty year renders the no-attendance empty state.
- **All endpoints GET/read-only** (SameSite=Lax CSRF rule); parent identity always from the JWT `sub` claim, never the request.
