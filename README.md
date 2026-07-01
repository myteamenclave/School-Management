# School Management System

A school management system covering student records, staff management, fee collection, and an admin overview dashboard, plus a parent portal with online payments and teacher attendance and gradebook tools. The data model is architected for multi-tenancy from day one.

See [docs/ideas/school-management-system.md](docs/ideas/school-management-system.md) for the full problem statement and scope rationale.

## Tech Stack

- **Backend:** .NET 8.0, Clean Architecture (Domain / Application / Infrastructure / WebApi), Entity Framework Core, PostgreSQL
- **Frontend (planned):** React 19, TypeScript, Vite, shadcn/ui + Tailwind CSS, TanStack Query
- **Auth:** JWT in httpOnly cookies, refresh-token rotation with theft detection (implemented)
- **Deployment:** Docker, GitHub Actions CI/CD

Full architectural detail lives in [.claude/context/architecture.md](.claude/context/architecture.md).

## Getting Started

### Prerequisites

- [Docker](https://www.docker.com/) — required either way (Postgres, the full stack, and the Testcontainers-based integration tests all use it)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — only needed if running the API outside Docker (e.g. for IDE debugging)

### Quickest: the whole stack via Docker Compose

```bash
cp .env.example .env   # then fill in real local values
docker compose up --build
```

This builds and runs `postgres` → `migrator` (applies migrations, seeds the demo school) → `api` → `nginx`, with proper startup ordering via `depends_on`. The API is reachable directly on `:8080` and through the nginx reverse proxy on `:8081` (port `80` is avoided locally — see `docker-compose.override.yml`, Windows often reserves it). On startup, `api` also seeds a demo Admin user — but **only** when `ASPNETCORE_ENVIRONMENT=Development` (the default for this local Compose flow; never in a `Production`-shaped run, including a real prod deploy). Login with it: `admin@demoschool.test` / `Passw0rd!`.

`docker-compose.yml` is the base (production-shaped: pre-built images, no published ports except via nginx); `docker-compose.override.yml` is auto-loaded for local dev (build-from-source, port mappings). Production runs explicitly with `docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.prod up -d`, which skips the dev override.

### Backend only, outside Docker (e.g. IDE debugging)

```bash
cd backend
dotnet restore

# Start just Postgres via Compose (or `docker run` directly)
docker compose up -d postgres

# Apply migrations
dotnet ef database update --project SchoolMgmt.Infrastructure --startup-project SchoolMgmt.Infrastructure

# Run the API
dotnet run --project SchoolMgmt.WebApi
```

The connection string is in `SchoolMgmt.WebApi/appsettings.Development.json` — update the port there if it doesn't match `.env`'s `POSTGRES_PORT`.

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
  Dockerfile                  Multi-stage: build / migrator / runtime
frontend/       React app (not yet started)
nginx/          Reverse proxy config (nginx.conf)
docs/           Business/technical decision records
specs/          Implementation specs
.claude/        Project context, rules, and catalog for AI-assisted development
docker-compose.yml            Base service definitions (prod-shaped)
docker-compose.override.yml   Local-dev overrides (auto-loaded)
docker-compose.prod.yml       Prod-only overrides (restart policy, public ports)
.env / .env.example / .env.prod   Environment values (.env / .env.prod gitignored)
```
