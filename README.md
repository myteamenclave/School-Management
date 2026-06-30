# School Management System

A school management system covering student records, staff management, and fee collection, plus a parent portal with online payments, a principal/owner dashboard, and teacher attendance and gradebook tools. The data model is architected for multi-tenancy from day one.

See [docs/ideas/school-management-system.md](docs/ideas/school-management-system.md) for the full problem statement and scope rationale.

## Status

| Spec | Covers | Status |
|---|---|---|
| [specs/01-implement-multi-tenant-data-model.md](specs/01-implement-multi-tenant-data-model.md) | Base entity conventions, EF Core tenant query filters, `ITenantProvider` abstraction, Repository/UnitOfWork pattern, seed school | ✅ Implemented |
| [specs/02-implement-auth.md](specs/02-implement-auth.md) | JWT-in-cookie auth, refresh rotation, RBAC, replaces the placeholder tenant provider with a real claims-based one | 📝 Specced, not yet implemented |

Frontend has not been started yet.

## Tech Stack

- **Backend:** .NET 8.0, Clean Architecture (Domain / Application / Infrastructure / WebApi), Entity Framework Core, PostgreSQL
- **Frontend (planned):** React 19, TypeScript, Vite, shadcn/ui + Tailwind CSS, TanStack Query
- **Auth (planned):** JWT in httpOnly cookies, refresh-token rotation with theft detection
- **Deployment:** Docker, GitHub Actions CI/CD

Full architectural detail lives in [.claude/context/architecture.md](.claude/context/architecture.md).

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) — used both for local Postgres and for the Testcontainers-based integration tests

### Backend

```bash
cd backend
dotnet restore

# Start a local Postgres (adjust the port if 5432 is already in use locally)
docker run -d --name schoolmgmt-pg-dev \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=schoolmgmt \
  -p 5432:5432 postgres:16-alpine

# Apply migrations
dotnet ef database update --project SchoolMgmt.Infrastructure --startup-project SchoolMgmt.Infrastructure

# Run the API
dotnet run --project SchoolMgmt.WebApi
```

The connection string is in `SchoolMgmt.WebApi/appsettings.Development.json` — update the port there if you started Postgres on something other than `5432`.

### Running tests

```bash
cd backend
dotnet test SchoolMgmt.slnx
```

Integration tests (`tests/SchoolMgmt.IntegrationTests`) spin up their own disposable Postgres container via Testcontainers — just make sure Docker is running. No manual database setup needed for tests.

## Project Documentation

This repo uses a structured documentation system designed for both humans and AI coding agents to ramp up quickly:

- **[CLAUDE.md](CLAUDE.md)** — start here. Indexes everything below.
- **`.claude/context/`** — current state: what the project is, the architecture, the database schema.
- **`.claude/rules/`** — enforceable coding rules (e.g. the GET-must-be-read-only CSRF rule, the Repository/UnitOfWork pattern).
- **`docs/`** — the *why*: business reasoning and trade-offs behind each decision.
- **`specs/`** — the *how*: a concrete implementation plan for every feature, written before the code.

## Project Structure

```
backend/        .NET 8 Clean Architecture solution
  SchoolMgmt.Domain/          Entities, no external dependencies
  SchoolMgmt.Application/     Use-case services, interfaces
  SchoolMgmt.Infrastructure/  EF Core, Postgres, external integrations
  SchoolMgmt.WebApi/          Thin controllers, composition root
  tests/                      xUnit unit + integration test projects
frontend/       React app (not yet started)
docs/           Business/technical decision records
specs/          Implementation specs
.claude/        Project context, rules, and catalog for AI-assisted development
```
