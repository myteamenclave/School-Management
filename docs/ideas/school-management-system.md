# School Management System — Capability Demo

## Problem Statement
How might we build a school management system MVP — covering students, staff, fees, and the people around them — that proves to a client (who gave us a 1-sentence brief) that we understood their problem better than they wrote it down?

## Recommended Direction
Build a single-school system, architecturally ready for multi-tenancy, that serves all three real stakeholders a school management system implies: **admin** (full-depth CRUD, operations, and oversight dashboard — the person who both runs and reviews the school's performance), **parent** (portal with grades/attendance visibility and online fee payment), and **teacher** (attendance marking and a lightweight gradebook).

The brief named three nouns — students, staff, fees. The pitch-winning move is showing the connective tissue between them: a teacher marks attendance → a parent sees it and pays a fee online → an admin sees both reflected in a live dashboard. That end-to-end coherence is more convincing than a long, disconnected feature list, even though the feature list itself is also broader than what was asked for.

**Note on roles:** Principal/Owner was originally a separate role with read-only dashboard access. Merged into Admin — in small schools the owner is typically the same person doing administration, and maintaining a separate read-only role adds complexity for minimal demo value.

Multi-tenancy is treated as an architecture decision, not a feature: every entity is scoped by `SchoolId` with EF Core global query filters from day one, but no tenant-onboarding UI, branding, or super-admin console gets built. One seeded school ships in the demo. This banks most of the "this could scale to a SaaS product" credibility for a small fraction of the engineering cost, preserving time for depth elsewhere.

## Key Assumptions to Validate
- [ ] Attendance + a lightweight gradebook are worth including even though the client didn't ask — validate by checking they don't crowd out fee/payment depth as the build proceeds; cut first if time runs short.
- [ ] A sandbox payment gateway (e.g., Stripe test mode) integration is achievable within the timeline alongside everything else — this is the single highest technical-risk item; spike it early, not last.
- [ ] "Architected for multi-tenancy, not built for it" reads as credible to the client rather than incomplete — be ready to explain the distinction clearly in the pitch/README.
- [ ] One human engineer + AI agent can deliver admin + parent + teacher surfaces at "reasonable depth" (per #7 domain lens: academic year/term, class/section, fee templates with installments/discounts, bulk import, audit trail) in 1-2 weeks — validate via daily scope checkpoints, not at the end.

## MVP Scope
**In:**
- Admin: student CRUD + bulk CSV import, staff CRUD, class/section assignment, academic year/term setup, fee structure templates (installments, discounts/scholarships), fee invoicing, financial audit trail, **overview dashboard** (revenue vs. outstanding fees, enrollment trend, attendance trend, staff overview)
- Parent portal: view child's grades/attendance/fee balance, pay fees online (sandbox payment gateway)
- Teacher: mark daily attendance, enter grades per subject/term (no timetable engine, no weighted GPA)
- Auth/RBAC across all three roles (Admin, Teacher, Parent)
- Multi-tenant-ready data model (`SchoolId` + global query filters), single seeded school in the demo
- Dockerized, CI/CD via GitHub Actions

**Out (see Not Doing):** library, transport, hostel, payroll, timetable scheduling engine, SMS notifications, multi-tenant onboarding/branding UI, mobile app

## Not Doing (and Why)
- **Full ERP modules (library, transport, hostel, payroll)** — high effort, not core to "students/staff/fees," and not visible enough in a 1-week demo to be worth the time.
- **Timetable/scheduling engine** — a deep feature on its own (constraint solving for room/teacher/period conflicts); a lightweight gradebook + attendance proves the teacher persona without this cost.
- **Multi-tenant onboarding UI, super-admin console, per-tenant branding** — invisible to a single-school demo; the architecture supports it later without this upfront cost.
- **SMS notifications** — email is enough to demonstrate the communication concept; SMS adds a paid third-party dependency for marginal demo value.
- **Mobile app** — a responsive web app covers parent/teacher/admin use cases for a capability demo; native apps are a different project.

## Open Questions
- Which payment gateway to spike first (Stripe vs. a regional/local provider) — depends on whether "generic/international" framing should still lean toward one ecosystem for demo speed.
- How much detail does the admin dashboard need (live data vs. seeded snapshot) given it's built last and depends on every other module having real data?
- Should the gradebook/attendance scope be confirmed or cut now, before architecture work starts, given it's the one assumption explicitly flagged as untested?
