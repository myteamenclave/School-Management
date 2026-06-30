<!-- Last verified: 2026-06-30. Update this file when the file is edited. -->

# Project: School Management System

## What it is

A school management system built as a capability demo: the client gave a one-sentence brief ("manage student details, staff details, and school fees") and the goal is to build a broader, coherent MVP ourselves and hand it to them for review, proving understanding of the domain beyond what was literally asked. Internal/B2B-style tool (admin and teacher users) with a public-facing surface (parent portal incl. online payment). Read/write mixed — CRUD-heavy for admin, who also has the overview dashboard. See [docs/ideas/school-management-system.md](../../docs/ideas/school-management-system.md) for the full idea doc and [docs/functionality-overview.md](../../docs/functionality-overview.md) for the feature checklist.

## Key Features

**Admin/Registrar management** — Student CRUD + bulk CSV import, staff CRUD, class/section assignment, academic year/term setup, fee structure templates (installments, discounts/scholarships), fee invoicing, financial audit trail. Full depth — this stays a "management" tool first.

**Parent Portal** *(planned)* — View child's grades/attendance/fee balance; pay fees online via a sandboxed payment gateway (Stripe test mode, default pick).

**Teacher tools** *(planned)* — Mark daily attendance; enter grades per subject/term (lightweight — no weighted GPA logic, no timetable engine).

**Admin Overview Dashboard** *(planned)* — Revenue vs. outstanding fees, enrollment trend, attendance trend, staff overview. Part of the Admin surface — built last since it depends on other modules having real data.

**Multi-tenant-ready data model** *(implemented)* — Every entity scoped by `SchoolId` with EF Core global query filters. Architecture only — no tenant-onboarding UI, branding, or super-admin console in v1.

**Auth & RBAC** *(implemented)* — JWT-in-httpOnly-cookie login/refresh/logout, refresh-token rotation with theft detection (reusing an already-rotated token revokes the whole session), role claims for `[Authorize(Roles = "...")]`. See [specs/02-implement-auth.md](../../specs/02-implement-auth.md).

## Core Business Rules

**Roles**
- `Admin` — full CRUD across students, staff, fees; manages academic year/term/class structure; has access to the overview dashboard (revenue, enrollment, attendance, staff overview). Principal/Owner merged into this role — in small schools the owner is typically also the administrator, and a separate read-only role adds complexity for minimal demo value.
- `Teacher` — marks attendance and enters grades for assigned classes/subjects only
- `Parent` — read access to their own child(ren)'s data only; can pay fees online

**Tenancy**
- Every entity is scoped to a `SchoolId`. One school is seeded for the demo, but query-level isolation is enforced from day one so the data model doesn't need an invasive migration later.
- `ITenantProvider.CurrentSchoolId` resolves from the authenticated user's JWT claims (`HttpContextTenantProvider`) and throws if accessed outside an authenticated request — login/refresh lookups (`IUserRepository.GetByEmailAsync`, `IRefreshTokenRepository.GetByTokenHashAsync`) are the deliberate, documented exception that bypass the tenant filter, since they run before a tenant is resolvable.

**Auth**
- GET endpoints must never have side effects (required by the `SameSite=Lax` cookie policy — see [.claude/rules/backend.md](../rules/backend.md)).
- No mediator library — Application-layer services are called directly via DI from controllers (see [.claude/rules/backend.md](../rules/backend.md)).
- **Demo login:** `admin@demoschool.test` / `Passw0rd!` — Admin role, tied to the seeded demo school. Only ever seeded in `Development` (`DemoDataSeeder`, gated by `IsDevelopment()`) — deliberately absent from any `Production`-environment deployment, including a real prod run of `docker-compose.yml`. See [specs/02-implement-auth.md](../../specs/02-implement-auth.md).

## Integrations

**Payment gateway** *(planned)*
- Purpose: online fee payment from the parent portal
- Provider: Stripe (test/sandbox mode), default pick — PayPal is the fallback if the client's market doesn't support Stripe well
- Notes: webhooks (server-to-server) are authenticated via the provider's signature header, not cookies; the browser redirect-back from checkout is why the auth cookie uses `SameSite=Lax` instead of `Strict`

## Known constraints & non-obvious decisions

- This is a capability-demo build for an unsolicited/under-specified brief — scope intentionally exceeds the literal client ask (parent portal, admin dashboard, attendance/gradebook) to demonstrate domain understanding, not because the client requested it.
- 1-2 week timeline, one human engineer (backend-focused) + AI agent. Functionalities are scoped down via discussion at implementation time if a given item proves too large — see [docs/functionality-overview.md](../../docs/functionality-overview.md).
- Deliberately out of scope for v1: library, transport, hostel, payroll modules; timetable/scheduling engine; SMS notifications; multi-tenant onboarding UI; native mobile app.
- No double-submit CSRF token in v1 — `SameSite=Lax` is treated as sufficient given the GET-must-be-read-only rule is enforced everywhere.
