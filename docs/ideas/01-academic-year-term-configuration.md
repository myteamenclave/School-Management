# Academic Year / Term Configuration

## Problem Statement
How might we give school admins a structured way to define academic years and
their semesters so that every other module (fees, grades, attendance, enrollment)
has a consistent, enforced time anchor to operate against?

## Recommended Direction
A two-level calendar structure: **AcademicYear** (e.g. "2024-2025") contains
exactly **two Semesters** (Semester 1, Semester 2), auto-scaffolded when the year
is created. Admin explicitly marks one year and one semester as "current" — no
automatic inference from dates. When a year is archived, a domain guard
(`EnsureNotArchived()`) prevents any downstream module from writing to it, giving
all future services a consistent, centralized enforcement contract rather than
a "remember to check" burden distributed across modules.

## Key Assumptions to Validate
- [ ] Two semesters per year is a fixed rule for this school — no trimester or
      quarter variation expected.
- [ ] Admin-managed transitions (explicit "set current", explicit "archive") are
      sufficient — no need for scheduled/automated year rollover.
- [ ] Light read-only enforcement (domain guard only, no DB-level constraint) is
      acceptable for v1 — full enforcement completes as each downstream module
      is built and calls the guard.

## MVP Scope
**In:**
- AcademicYear CRUD (create, read, list, archive) — no delete once referenced
- Auto-scaffold Semester 1 and Semester 2 on year creation; admin fills in dates
- Semester date editing (name, start date, end date)
- Set current year (single active at a time per school)
- Set current semester (single active at a time per school)
- `AcademicYearStatus` enum: `Active | Archived`
- Domain guard: `AcademicYear.EnsureNotArchived()` — throws `DomainException` if
  archived; called by every downstream Application service before a write
- Setting a new year as current does NOT auto-archive the old one — archiving is
  an explicit, separate admin action
- Setting a year as current auto-sets its Semester 1 as the current semester;
  admin can override to a different semester afterward

**Out (see Not Doing):** year deletion, semester deletion, date-based
auto-current inference, custom term counts, bulk year setup wizard

## Not Doing (and Why)
- **Year/semester deletion** — once a year is created and referenced by any
  downstream record it cannot be safely deleted; archiving is the safe lifecycle
  end state.
- **Automatic "current" based on date ranges** — adds fallback logic and edge
  cases (what if today is between years?) for minimal UX benefit in an admin tool.
- **Custom term counts (3 terms, quarters)** — the target school uses 2 semesters;
  flexible term counts add config surface with no immediate payoff.
- **Bulk/wizard year setup** — a single create form is sufficient for v1 admin
  usage and demo depth.

## Open Questions
- ~~Should setting a year as current auto-set its Semester 1 as the current
  semester?~~ Resolved: yes — auto-set Semester 1, admin can override.
