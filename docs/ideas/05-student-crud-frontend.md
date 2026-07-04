# Student CRUD — Frontend

## Problem Statement
How might we build an Admin-only Student management UI that lets school staff
efficiently browse, create, and update hundreds of student records — including
enrollment status lifecycle and guardian contacts — within the same clean shell
pattern already used by Academic Years and Grades?

## Recommended Direction

A **paginated data table** at `/admin/students` with:

- **Status tabs** (Active / Transferred / Graduated / Dropped) that drive the
  `?status=` API param — Active is default
- **Debounced name search** (300 ms) that sends `?search=` to the API; requires
  a small backend addition (ILIKE filter on FirstName + LastName in
  `StudentRepository.GetPagedAsync`)
- **Modal dialogs** for Create and Edit — consistent with Academic Years and
  Grades pages; the form has ~13 fields total (split into a core section and a
  collapsible Guardian section)
- **Traditional pagination** (Prev / page numbers / Next) using the existing
  `PagedResult<T>` API shape (TotalCount + Page + PageSize)

Key asymmetries between Create and Edit:
- **Create:** no `EnrollmentStatus` field — always starts as `Active`
- **Edit:** includes `EnrollmentStatus` dropdown (Active / Transferred /
  Graduated / Dropped) and a read-only `StudentCode` badge
- `StudentCode` is never editable — shown as a dimmed badge in the edit header

## Key Assumptions to Validate
- [x] `GET /api/students` already supports `?status=`, `?page=`, `?pageSize=`
      — confirmed in spec 05
- [ ] Adding `?search=` to `GET /api/students` — a one-liner WHERE clause added
      to `StudentRepository.GetPagedAsync`; no migration needed (filter-only,
      no schema change)
- [x] `shadcn/ui Table` component is not yet installed in the frontend — needs
      `npx shadcn@latest add table` during implementation

## MVP Scope

**In:**
- `/admin/students` route with paginated table, status tabs, name search
- Create Student modal (First/Last name, DOB, Gender, Enrollment date, guardian
  fields — no status)
- Edit Student modal (same fields + EnrollmentStatus dropdown, StudentCode
  read-only)
- Students entry in AppShell sidebar nav
- Backend: add `?search=` ILIKE filter to `StudentRepository` (no migration)

**Out for MVP (but tracked in functionality-overview.md):**
- Student detail page (edit modal covers all fields)
- Bulk CSV import
- Class/section assignment (explicitly deferred in spec 05)
- Photo upload
- Export to CSV/PDF

## Not Doing (and Why)
- **Hard delete button** — backend explicitly has no DELETE route; by design,
  status transitions replace deletion
- **Dedicated student detail page** — the edit modal exposes all fields; a
  separate detail page adds navigation overhead with no new information
- **Inline status toggle in table row** — too easy to accidentally change;
  status transitions belong in the Edit modal where they're intentional
- **Client-side-only search** — breaks for schools with >20 students on page;
  server-side ILIKE is the right call

## Open Questions
- Pagination style: simple Prev/Next with page number display, or numbered page
  buttons (1 2 3 … 10)? Default to Prev/Next for simplicity unless there's a
  reason for numbered pages.
