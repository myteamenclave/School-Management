# CLAUDE.md

School Management System — a capability-demo build (see [docs/ideas/school-management-system.md](docs/ideas/school-management-system.md) for the full story). Backend: .NET 8.0, Clean Architecture, EF Core, PostgreSQL. Frontend: React 19, TypeScript, shadcn/ui + Tailwind. Read this file first in any new session — it indexes everything else.

## Document Index

### Project Context — `.claude/context/`
Read these first. They describe the current state of the project — what it is, how it's built, and why.

| File | Purpose |
|---|---|
| [.claude/context/project.md](.claude/context/project.md) | What the project is, key features, core business rules, integrations, known constraints |
| [.claude/context/architecture.md](.claude/context/architecture.md) | Tech stack, folder structure, system design, authentication flow, key architectural decisions |
| [.claude/context/database.md](.claude/context/database.md) | DB engine, schema (filled in as tables are migrated), migration strategy |

### Business & Technical Decisions — `docs/`
Canonical record of *why* the project is shaped the way it is. Read these to understand the business and technical reasoning behind a decision, not just the decision itself.

| File | Purpose |
|---|---|
| [docs/ideas/school-management-system.md](docs/ideas/school-management-system.md) | Source idea doc — problem statement, recommended direction, key assumptions, MVP scope, what's deliberately not being built |
| [docs/ideas/01-academic-year-term-configuration.md](docs/ideas/01-academic-year-term-configuration.md) | Academic year / term configuration — two-level calendar structure, semester auto-scaffold, domain guard enforcement |
| [docs/ideas/02-class-section-structure.md](docs/ideas/02-class-section-structure.md) | Class / section structure — persistent Grade + Section hierarchy, Admin CRUD, delete guards |
| [docs/ideas/05-student-crud-frontend.md](docs/ideas/05-student-crud-frontend.md) | Student CRUD frontend — paginated table, status tabs, debounced server-side search, modal create/edit |
| [docs/ideas/06-subject-management.md](docs/ideas/06-subject-management.md) | Subject catalog — flat school-wide subject list, code immutability rationale, not-doing list |
| [docs/functionality-overview.md](docs/functionality-overview.md) | Master goal list / checklist of every functionality across all modules — the index of what's left to build |
| [docs/design-system.md](docs/design-system.md) | Color palette, typography, design vibe — source of truth before syncing to Claude Design |

Each functionality in `docs/functionality-overview.md` gets its own idea doc under `docs/ideas/<functionality-name>.md` when it's picked up for implementation, before its spec is written.

### Implementation Specs — `specs/`
Concrete technical plans for *how* a feature is actually implemented. **Every feature has a spec** — a feature only skips a `docs/` entry if it's simple/obvious, but it never skips a spec. Always implement against the spec, not from memory or assumption.

| File | Purpose | Status |
|---|---|---|
| [specs/01-implement-multi-tenant-data-model.md](specs/01-implement-multi-tenant-data-model.md) | Base entity conventions, EF Core global query filters, `ITenantProvider` abstraction, seed school | Implemented |
| [specs/02-implement-auth.md](specs/02-implement-auth.md) | JWT-in-cookie auth, refresh rotation + theft detection, RBAC, real claims-based `ITenantProvider` | Implemented |
| [specs/03-implement-academic-year-term-configuration.md](specs/03-implement-academic-year-term-configuration.md) | Academic year + semester entities, domain guard (`EnsureNotArchived`), Admin CRUD API | Implemented |
| [specs/04-implement-class-section-structure.md](specs/04-implement-class-section-structure.md) | Grade + Section entities, per-grade section CRUD, `EnsureNoSections` delete guard, Admin CRUD API | Implemented |
| [specs/05-implement-student-crud.md](specs/05-implement-student-crud.md) | Student entity, `StudentCode` auto-generation (YYYY-NNNNNN), Admin CRUD, inline guardian fields, `EnrollmentStatus` lifecycle, no hard delete | Implemented |
| [specs/02-B-scaffold-frontend-and-auth.md](specs/02-B-scaffold-frontend-and-auth.md) | React 19 + Vite scaffold, Zustand auth store, Axios 401-refresh interceptor, login page, app shell, role-aware route guards | Implemented |
| [specs/06-B-implement-student-crud-frontend.md](specs/06-B-implement-student-crud-frontend.md) | Student CRUD frontend — paginated table, status tabs, debounced server-side search (`?search=` ILIKE backend addition), modal create/edit, Prev/Next pagination | In progress |
| [specs/07-B-implement-teacher-crud-frontend.md](specs/07-B-implement-teacher-crud-frontend.md) | Teacher CRUD frontend — paginated table, Active/Inactive/All tabs, debounced search (`?search=` ILIKE backend addition), modal create/edit, IsActive toggle | Implemented |
| [specs/07-implement-subject-management.md](specs/07-implement-subject-management.md) | Subject entity, Admin CRUD, unique code per school, `IsActive` soft-disable, paged list with ILIKE search | Implemented |

### Coding Rules — `.claude/rules/`
Enforceable coding rules an agent must follow while writing code, not background context.

| File | Purpose |
|---|---|
| [.claude/rules/backend.md](.claude/rules/backend.md) | GET-must-be-side-effect-free (CSRF/SameSite rule), thin-controller/Application-service pattern |
| `.claude/rules/frontend.md` | Frontend coding rules — not yet created |

## Working Conventions

- **Naming:** docs are numbered nouns, e.g. `docs/ideas/01-student-management.md`; specs are numbered verb-led actions, e.g. `specs/01-implement-auth.md`. Two-digit zero-padded sequence number, kebab-case, incrementing independently within each of `docs/ideas/` and `specs/`.
- **`docs/` vs `specs/`:** `docs/` answers *why* (business reasoning, trade-offs, decisions); `specs/` answers *how* (concrete implementation plan for one feature). A doc is optional for trivial features; a spec is not optional for any feature.
- **A spec must always reference its related docs and specs.** Link the `docs/` entries it implements and any prior `specs/` it builds on or depends on, so reading the spec alone doesn't lose the *why* behind its decisions.
- Before starting any work, check `.claude/context/*.md` for the current architecture/project state — it's kept up to date as decisions are made.
- **When writing code, you must always follow the rules.** Rules for backend are in [.claude/rules/backend.md](.claude/rules/backend.md). Rules for frontend are in `.claude/rules/frontend.md`.
- When a new context file, doc, spec, or rule file is added, update this index in the same change — a stale index is worse than no index.
