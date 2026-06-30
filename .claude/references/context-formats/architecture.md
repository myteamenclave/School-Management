<!-- Last verified: [DATE]. Update this line whenever the architecture changes. -->

# Architecture

<!-- One-line summary of each: architectural style, major modules, and integration approach. Example:
- Style: Feature-based frontend with service layer
- Modules: Auth, Dashboard, [Domain A], [Domain B]
- Integration: SSR/CSR hybrid pages with REST APIs
-->

## Tech Stack

<!-- Fill in one row per layer. Remove rows that don't apply; add rows for layers not listed. -->

| Layer | Technology | Language | Version | Notes |
|---|---|---|---|---|
| Frontend | <!-- e.g. Next.js (App Router) --> | <!-- e.g. TypeScript --> | <!-- e.g. 14 --> | <!-- key constraint or gotcha --> |
| UI components | | | | |
| Styling | | | | |
| Backend | | | | |
| Database | | | | |
| ORM | | | | |
| Auth | | | | |
| Runtime | | | | |

## Testing

<!-- Describe the testing setup. E2E testing always uses Playwright. -->

| Type | Tool | Scope | Notes |
|---|---|---|---|
| Unit | <!-- e.g. Jest --> | <!-- e.g. service functions, hooks --> | <!-- e.g. mock DB at module level --> |
| Integration | <!-- e.g. Jest + Supertest --> | <!-- e.g. API routes against a test DB --> | <!-- e.g. runs against a real DB in CI --> |
| E2E | Playwright | <!-- e.g. full user flows in a browser --> | <!-- e.g. Page Object Model, data-testid selectors only --> |

<!-- Add a short note on any non-obvious testing conventions: what is mocked, what hits a real DB, how test data is seeded, etc. -->

## Folder Structure

```
<!-- Paste the actual directory tree here. Annotate every non-obvious folder with a comment.
Keep it up to date — stale structure is worse than no structure. -->
```

**Rules enforced by this structure:**
<!-- List the invariants that the folder layout enforces. Example:
- `features/<module>/services/` files are server-only — never import from `"use client"` files.
- API route handlers import from `services/` only — never query the DB directly.
-->

## System Design

### Request lifecycle

<!-- Number each step. Describe how a request travels from the client to the database and back. Cover: client call, middleware, route handler, service layer, DB, and response shape. Example:

1. **Browser sends request** — ...
2. **Middleware intercepts** — ...
3. **Route handler** — ...
4. **Service layer** — ...
5. **Response** — all API routes return JSON. Success: `{ data: ... }`. Errors: `{ error: string }`.
-->

---

### Authentication

<!-- Describe the full auth flow: login, per-request verification, token refresh, logout, and authorization. Cover which tokens are used, where they're stored, and what happens at each step. -->

---

### Main data flow

<!-- Describe the most complex or important data flow in the application in detail. Walk through each step: user action → hook/fetch → API route → service → DB → response → render. If there are multiple complex flows, add subsections. -->

---

### Background jobs & Async work

<!-- List any work that happens outside the request/response cycle: cron jobs, queues, webhooks, email workers, etc. For each: trigger, worker mechanism, and failure handling. If none exist, write:
"No background jobs are currently implemented. Add entries here if async work is introduced." -->

## Key Decisions & Why

<!-- For each significant architectural decision, explain what was chosen and why. Include the trade-off that was accepted. Format:

**Decision title**
What was chosen and why. The trade-off: [what you gave up].
-->
