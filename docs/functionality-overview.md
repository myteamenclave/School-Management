# Functionality Overview — School Management System

Master goal list for the capability-demo build. This is the index, not the detail — when a functionality is picked up for implementation, it gets its own idea doc under `docs/ideas/<functionality-name>.md` (via `/agent-skills:idea-refine`) before coding starts.

Source direction: [school-management-system.md](ideas/school-management-system.md)

Status legend: `[ ]` not started · `[~]` in progress / has an idea doc · `[x]` done

Each feature is split into backend (API) and frontend (UI) since they progress independently.

## Platform / Cross-Cutting
- Multi-tenant-ready data model (`SchoolId` on every entity, EF Core global query filters) — architecture only, no tenant-onboarding UI
  - [x] Backend
- Auth & RBAC (Admin, Teacher, Parent roles — Principal merged into Admin)
  - [x] Backend
  - [x] Frontend (login page, token refresh, route guards)
- Academic year / term configuration — [idea doc](ideas/01-academic-year-term-configuration.md)
  - [x] Backend
  - [ ] Frontend
- Class / section structure — [idea doc](ideas/02-class-section-structure.md)
  - [x] Backend
  - [ ] Frontend
- Docker + CI/CD
  - [x] Docker (backend Dockerfile + layered docker-compose)
  - [ ] CI/CD pipeline (GitHub Actions)

## Admin / Registrar
- Student CRUD — [idea doc](ideas/03-student-crud.md)
  - [x] Backend
  - [ ] Frontend
- Bulk student/teacher import (CSV)
  - [ ] Backend
  - [ ] Frontend
- Teacher management (Admin creates/manages teacher accounts — name, contact, assigned subjects/classes)
  - [ ] Backend
  - [ ] Frontend
- Class/section assignment (students to classes, teachers to subjects/classes)
  - [ ] Backend
  - [ ] Frontend
- Fee structure templates (installments, discounts, scholarships)
  - [ ] Backend
  - [ ] Frontend
- Fee invoicing
  - [ ] Backend
  - [ ] Frontend
- Financial audit trail (who changed/collected what, when)
  - [ ] Backend
  - [ ] Frontend
- Overview dashboard (revenue vs. outstanding fees, enrollment trend, attendance trend, teacher overview)
  - [ ] Backend
  - [ ] Frontend

## Parent Portal
- Parent login linked to child/children
  - [ ] Backend
  - [ ] Frontend
- View child's grades
  - [ ] Backend
  - [ ] Frontend
- View child's attendance
  - [ ] Backend
  - [ ] Frontend
- View fee balance / invoices
  - [ ] Backend
  - [ ] Frontend
- Pay fees online (sandbox payment gateway — Stripe test mode by default)
  - [ ] Backend
  - [ ] Frontend

## Teacher
- Mark daily attendance
  - [ ] Backend
  - [ ] Frontend
- Enter grades per subject/term (lightweight — no weighted GPA logic)
  - [ ] Backend
  - [ ] Frontend

## Explicitly Out of Scope (see "Not Doing" in the source idea doc)
- Library, transport, hostel, payroll modules
- Timetable / scheduling engine
- SMS notifications
- Multi-tenant onboarding UI, super-admin console, per-tenant branding
- Native mobile app

## Open / To Confirm Before Build
- [ ] Payment gateway final pick (defaulting to Stripe test mode; confirm before that module starts)
- [ ] Admin dashboard data freshness (live query vs. periodic snapshot) — decide once other modules have real data
