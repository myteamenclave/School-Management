<!-- Last verified: 2026-06-30. Update this line whenever the architecture changes. -->

# Architecture

- Style: Clean Architecture backend (Domain / Application / Infrastructure / WebApi) with a feature-based React frontend; controllers stay thin via direct DI into Application-layer services (no mediator library — see [.claude/rules/backend.md](../rules/backend.md))
- Modules: Students, Staff, Fees (incl. online payment), Parent Portal, Teacher (attendance + gradebook), Principal Dashboard
- Integration: SPA (React) calling a REST API (ASP.NET Core), JWT auth via httpOnly cookies, multi-tenant-ready data model (single tenant seeded for the demo)

## Tech Stack

| Layer | Technology | Language | Version | Notes |
|---|---|---|---|---|
| Frontend | React | TypeScript | React 19 | Vite build tool |
| UI components | shadcn/ui + Tailwind CSS | — | — | Composable primitives; chosen over Ant Design/Mantine for full design control across admin-dense vs. parent-friendly screens |
| Styling | Tailwind CSS | — | — | See [docs/design-system.md](../../docs/design-system.md) for palette/typography |
| State (server) | TanStack Query (`@tanstack/react-query`) | — | — | Formerly "React Query" — same library, renamed |
| State (client) | Zustand | — | — | Only for auth session/UI state; server state lives in TanStack Query |
| Forms | React Hook Form + Zod | — | — | Zod schemas should mirror backend DTOs where practical |
| Backend | ASP.NET Core, Clean Architecture | C# | .NET 8.0 | Domain / Application / Infrastructure / WebApi projects |
| Database | PostgreSQL | — | — | Every entity scoped by `SchoolId` (multi-tenant-ready; no tenant-onboarding UI in v1) |
| ORM | Entity Framework Core | — | — | EF Core global query filters enforce tenant scoping |
| Auth | JWT (access + refresh) in httpOnly cookies | — | — | See Authentication section below |
| Runtime / Deployment | Docker, GitHub Actions CI/CD | — | — | |

## Testing

| Type | Tool | Scope | Notes |
|---|---|---|---|
| Unit (backend) | xUnit | Domain entities/business rules, Application-layer services | No mocking library — dependencies are faked with hand-written stub implementations of the same interfaces, not Moq/NSubstitute. Plain `Assert.*`, no FluentAssertions. |
| Integration (backend) | xUnit + `WebApplicationFactory` | API endpoints against a real Postgres instance | Real Postgres via Testcontainers (spun up per test run), not EF Core's InMemory provider — InMemory doesn't enforce constraints or real SQL translation and hides real bugs. Docker is already in the stack, so no new infra dependency. |
| Unit (frontend) | Jest + React Testing Library | Components, hooks, utils | jsdom environment; RTL favors testing user-facing behavior over implementation detail |
| E2E | Playwright | Full user flows across personas (admin/teacher/parent/principal) | Mandated regardless of other choices |

## Folder Structure

```
backend/
  SchoolMgmt.Domain/          # Entities, value objects, domain logic — no dependencies on other layers
  SchoolMgmt.Application/     # Use-case services (one per feature area, e.g. StudentService), DTOs, validation
  SchoolMgmt.Infrastructure/  # EF Core, Postgres, external integrations (payment gateway, etc.)
  SchoolMgmt.WebApi/          # Controllers (thin — DI into Application services only), auth middleware
frontend/                     # React 19 + Vite app (structure TBD as build starts)
```

**Rules enforced by this structure:**
- Domain has no dependencies on Application/Infrastructure/WebApi.
- Controllers in WebApi call Application-layer services only — no direct EF Core/DbContext access from controllers.
- No mediator library (no MediatR) — see [.claude/rules/backend.md](../rules/backend.md) for the Application-layer pattern.

## System Design

### Request lifecycle

<!-- To be filled in once the first end-to-end slice (e.g. student enrollment) is implemented. -->

---

### Authentication

JWT-based, with both tokens delivered as httpOnly cookies (never exposed to JavaScript, never stored in localStorage).

1. **Login** — client POSTs credentials; server validates and issues two cookies: a short-lived **access token** (JWT) and a longer-lived **refresh token** (opaque or JWT — backed by a DB record either way).
2. **Cookie attributes** — both cookies are `httpOnly`, `Secure`, `SameSite=Lax`. `Lax` (not `Strict`) was chosen deliberately: it still blocks cross-site `POST`/`PUT`/`PATCH`/`DELETE`/`fetch` (the real CSRF vector), but allows the cookie on a top-level GET redirect — needed because the payment flow redirects the parent's browser back from the payment gateway (Stripe) to the app. See [.claude/rules/backend.md](../rules/backend.md) for the resulting GET-must-be-read-only rule this implies.
3. **Per-request verification** — the access token JWT is validated by ASP.NET Core's standard JWT middleware on every request (signature, expiry, claims) — no DB hit needed for normal requests.
4. **Token refresh** — when the access token expires, the client calls a refresh endpoint. The server validates the refresh token against a **DB-backed allowlist** (Postgres table: hashed token, user/session id, expiry, revoked flag, replaced-by pointer), then **rotates** it: issues a new access + refresh token pair and marks the old refresh token as used/replaced. If an already-rotated (used) refresh token is presented again, treat it as token theft and revoke the entire session family.
5. **Logout** — clears the cookies client-side AND revokes the refresh token server-side (DB row marked revoked) — a JWT can't be invalidated before expiry by cookie-clearing alone, so server-side revocation is required for real logout.
6. **Authorization** — role-based (Admin, Teacher, Principal/Owner, Parent), claims embedded in the access token JWT; per-tenant scoping (`SchoolId`) is enforced separately via EF Core global query filters, not via the JWT.
7. **CSRF** — no double-submit token for v1; `SameSite=Lax` is considered sufficient given GET stays side-effect-free everywhere (per the backend rule above). Webhooks from the payment provider (server-to-server, e.g. Stripe) are authenticated via the provider's signature header, not cookies — unrelated to this flow.

---

### Main data flow

<!-- To be filled in once the first end-to-end slice is implemented. -->

---

### Database migrations

**Local development**
- Migration commands (`dotnet ef migrations add <Name>`, `dotnet ef database update`) are run against `SchoolMgmt.Infrastructure` — that's where `DbContext` and the migrations live — never against `SchoolMgmt.WebApi`.
- The connection string lives in `SchoolMgmt.WebApi`'s `appsettings.Development.json`, not in Infrastructure, so the EF Core CLI tooling has no `Program.cs`/DI container to pull a connection string from when invoked against Infrastructure directly. Infrastructure implements `IDesignTimeDbContextFactory<TContext>` — EF Core's CLI tooling auto-discovers this interface and uses it to construct the `DbContext` at design time (migrations, scaffolding) independent of the WebApi host. This avoids having to spin up the full WebApi just to author a migration.

**Deployment**
- The backend `Dockerfile` is multi-stage: `build` (SDK image, restore + publish), `migrator` (built from `build`, installs the `dotnet-ef` CLI tool and runs `dotnet ef database update` against Infrastructure as its entrypoint), and `runtime` (ASP.NET runtime image only, no SDK/build tools, runs the published WebApi).
- The `migrator` stage is built as its own image and run as a separate one-shot container in `docker-compose` — it applies pending migrations and exits, distinct from the long-running API container. Compose sequences the API container to start only after the migrator container completes successfully (`depends_on` with `condition: service_completed_successfully`).
- If a background-worker project is introduced later (none exists yet — see Background jobs below), it gets its own `publish` + `runtime` stage following the same pattern as the API.

```dockerfile
# --- build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /App

# Copy .csproj and restore as distinct layers for better caching
COPY SchoolMgmt.slnx ./
COPY SchoolMgmt.WebApi/SchoolMgmt.WebApi.csproj ./SchoolMgmt.WebApi/
COPY SchoolMgmt.Domain/SchoolMgmt.Domain.csproj ./SchoolMgmt.Domain/
COPY SchoolMgmt.Application/SchoolMgmt.Application.csproj ./SchoolMgmt.Application/
COPY SchoolMgmt.Infrastructure/SchoolMgmt.Infrastructure.csproj ./SchoolMgmt.Infrastructure/

# Restore dependencies
RUN dotnet restore ./SchoolMgmt.slnx

# Copy the rest of the project and publish the application
COPY . ./
RUN dotnet publish -c Release -o published ./SchoolMgmt.WebApi/SchoolMgmt.WebApi.csproj

# --- migrator stage
FROM build AS migrator
WORKDIR /App
RUN dotnet tool install --global dotnet-ef --version 8.*
ENV PATH="${PATH}:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "./SchoolMgmt.Infrastructure/SchoolMgmt.Infrastructure.csproj"]

# --- api runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /App
COPY --from=build /App/published .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "SchoolMgmt.WebApi.dll"]
```

---

### Background jobs & Async work

No background jobs are currently implemented. Add entries here if async work is introduced (e.g. fee-due reminder emails, if added later).

## Key Decisions & Why

**No mediator library (no MediatR)**
Controllers call Application-layer services directly via DI instead of a Command/Handler mediator pattern. The trade-off: no built-in pipeline-behavior mechanism for cross-cutting concerns — validation/logging/transactions are implemented as ASP.NET Core filters and in-service logic instead. See [.claude/rules/backend.md](../rules/backend.md).

**Multi-tenancy architected, not built**
Every entity carries a `SchoolId` with EF Core global query filters enforcing tenant scoping, but no tenant-onboarding UI, branding, or super-admin console is built for v1 — one seeded school ships in the demo. The trade-off: the system *looks* single-tenant in the demo even though the data layer supports more; this is a deliberate scope cut to protect time for admin/parent/principal depth.

**JWT in httpOnly cookies, `SameSite=Lax`, DB-backed refresh rotation**
Chosen over localStorage-stored tokens (XSS risk) and over `SameSite=Strict` (breaks the payment-gateway redirect-back UX). The trade-off: refresh isn't purely stateless — a DB-backed allowlist is required for real revocation and theft detection, adding a small amount of backend state to an otherwise mostly-stateless auth scheme.
