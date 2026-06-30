# Functionality Overview — School Management System

Master goal list for the capability-demo build. This is the index, not the detail — when a functionality is picked up for implementation, it gets its own idea doc under `docs/ideas/<functionality-name>.md` (via `/agent-skills:idea-refine`) before coding starts.

Source direction: [school-management-system.md](ideas/school-management-system.md)

Status legend: `[ ]` not started · `[~]` in progress / has an idea doc · `[x]` done

## Platform / Cross-Cutting
- [x] Multi-tenant-ready data model (`SchoolId` on every entity, EF Core global query filters) — architecture only, no tenant-onboarding UI
- [x] Auth & RBAC (Admin, Teacher, Parent roles — Principal merged into Admin)
- [ ] Academic year / term configuration
- [ ] Class / section structure
- [~] Docker + CI/CD (GitHub Actions) — Docker (backend Dockerfile + layered docker-compose) done; CI/CD pipeline not yet started

## Admin / Registrar
- [ ] Student CRUD
- [ ] Bulk student/staff import (CSV)
- [ ] Staff CRUD
- [ ] Class/section assignment (students to classes, staff to subjects/classes)
- [ ] Fee structure templates (installments, discounts, scholarships)
- [ ] Fee invoicing
- [ ] Financial audit trail (who changed/collected what, when)
- [ ] Overview dashboard (revenue vs. outstanding fees, enrollment trend, attendance trend, staff overview)

## Parent Portal
- [ ] Parent login linked to child/children
- [ ] View child's grades
- [ ] View child's attendance
- [ ] View fee balance / invoices
- [ ] Pay fees online (sandbox payment gateway — Stripe test mode by default)

## Teacher
- [ ] Mark daily attendance
- [ ] Enter grades per subject/term (lightweight — no weighted GPA logic)

## Explicitly Out of Scope (see "Not Doing" in the source idea doc)
- Library, transport, hostel, payroll modules
- Timetable / scheduling engine
- SMS notifications
- Multi-tenant onboarding UI, super-admin console, per-tenant branding
- Native mobile app

## Open / To Confirm Before Build
- [ ] Payment gateway final pick (defaulting to Stripe test mode; confirm before that module starts)
- [ ] Principal dashboard data freshness (live query vs. periodic snapshot) — decide once other modules have real data
