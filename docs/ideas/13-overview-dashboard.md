# Overview Dashboard

> Related: [functionality-overview.md](../functionality-overview.md) (Admin / Registrar → "Overview dashboard"). Reads from the fee invoicing ([08](08-fee-invoicing.md)), class/section assignment ([09](09-class-section-assignment.md)), attendance ([11](11-attendance-marking.md)), and student modules — it aggregates their data, adds no new source-of-truth entities.

## Problem Statement
How might we give a school admin a single at-a-glance screen that answers "is the school financially healthy, well-attended, appropriately staffed — and what needs my attention right now?" using only data the system already holds?

## Recommended Direction
An **Admin-only** dashboard that **replaces the current empty post-login landing page** ([DashboardPage.tsx](../../frontend/src/pages/dashboard/DashboardPage.tsx)). It's organized around **signal, not vanity**: the top surfaces exceptions an admin should act on, and charts below provide context. Every tile drills through to the module it summarizes, making the dashboard the hub of the admin app rather than a dead-end poster.

All numbers are computed **live on load** via a single aggregation endpoint (`GET /api/dashboard/overview`) — appropriate at demo data volumes, always fresh, and no background-job infrastructure to build. This resolves open question #91 (data freshness) in favor of live query. Charts use **shadcn/ui's Chart component (Recharts)**, matching the existing design system.

The screen is scoped to a **selected academic year** via a **year selector** at the top; the endpoint takes `?academicYearId=` and defaults to the active year when omitted. Time-series charts use **monthly** buckets.

The four required areas map to what the data can *honestly* support:
- **Finance (hero)** — from `FeeInvoiceInstallment`: Collected (Σ `AmountPaid`), Outstanding (issued-but-unpaid), Overdue (past `DueDate`, unpaid), and a collection-rate %. Plus a **monthly collected-vs-billed** chart (real time-series, keyed on `PaidAt` / `DueDate`).
- **Attendance trend** — from `AttendanceRecord`: a **monthly present-rate line** (the one genuine trend), computed as (Present+Late) / total per month.
- **Enrollment** — a **breakdown, not a trend**: current active count + bars by grade and by `EnrollmentStatus`. (Enrollment is keyed per academic year, so a time-series line would be 1–2 dishonest points.)
- **Teacher overview** — a **coverage snapshot**: teacher headcount, total assignments, and **sections/subjects with no teacher assigned** (the actionable gap).

## Key Assumptions to Validate
- [ ] **Seed data is rich enough to look alive** — enough paid/overdue installments and dated attendance records that charts aren't near-empty. *Test: run the aggregation against seeded data and eyeball each tile before building UI.*
- [ ] **Live aggregation is fast enough** — the endpoint stays well under ~300ms at demo volume. *Test: time the query; almost certainly fine at this scale, but confirm the `GROUP BY`s.*
- [ ] **"Active academic year" is unambiguously resolvable** — the year selector defaults to a single canonical active year the backend can pick. *Test: confirm there's one active year, or define the selection rule (e.g. latest non-archived).*
- [ ] **shadcn Chart + Recharts installs cleanly** into the React 19 / Vite / TanStack stack. *Test: add the dependency and render one throwaway chart first.*

## MVP Scope
**In:**
- One backend aggregation endpoint (`GET /api/dashboard/overview?academicYearId=`, Admin-only, read-only) returning a single typed DTO for the whole screen. Defaults to active year when the param is omitted.
- Academic-year selector at the top of the page, defaulting to the active year.
- Finance KPI row + monthly collected-vs-billed chart.
- Attendance monthly present-rate line chart.
- Enrollment breakdown (by grade, by status).
- Teacher coverage snapshot (headcount, assignments, unassigned sections/subjects count).
- Drill-through links from each tile into its existing module page.
- Loading skeletons + graceful empty states per tile.

**Out:**
- Date-range picker, cross-filtering, custom widgets.
- Cached/snapshot pipeline or any background job.
- Per-role variants (no parent/teacher dashboard here).
- Export (PDF/CSV).
- Real-time push / auto-refresh.

## Not Doing (and Why)
- **Enrollment as a time-series line** — data is per-academic-year; a "trend" would be 1–2 points and dishonest. Breakdown tells the truth.
- **Cached snapshots** — premature at demo scale; live query is simpler and always fresh (resolves open question #91 in favor of live).
- **Date-range / interactive BI controls** — turns a demo dashboard into a reporting tool; unbounded scope for near-zero demo value. The year selector is the only filter.
- **Financial audit-trail integration** — that's a separate un-built module (overview bullet ≠ audit bullet); keep them decoupled.

## Settled Decisions
- **Time granularity:** monthly buckets for both finance and attendance charts.
- **Scoping control:** an academic-year selector (not silent active-year); endpoint takes `?academicYearId=`, defaults to active year.

## Next Step
`/agent-skills:spec` → `specs/16-implement-overview-dashboard.md`, referencing this doc and the source specs it aggregates (08/12 fee invoicing, 10/11 assignment, 14 attendance, 05 student CRUD).
