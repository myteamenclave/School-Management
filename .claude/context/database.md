<!-- Last verified: 2026-06-30. Update this line whenever the schema changes. -->

# Database

- Engine: PostgreSQL
- Schema approach: Relational schema, multi-tenant-ready — every table carries a `SchoolId` column, enforced via EF Core global query filters rather than separate databases/schemas per tenant.

## Schema

Column table format: **Column** | **PG Type** | **Max Length** | **Default** | **Nullable** | **Key** | **Constraints** | **Description**

---

### `Schools`

The tenant entity itself — not `ITenantScoped` (a school doesn't belong to itself), so no `SchoolId` column here.

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | Surrogate primary key |
| `Name` | `varchar` | 200 | — | NOT NULL | — | — | School name |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | Row creation timestamp, stamped by `AppDbContext.SaveChangesAsync` |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | Last update timestamp, stamped by `AppDbContext.SaveChangesAsync` |

Seeded with one row via the `InitialCreate` migration's `HasData`: `Id = 00000000-0000-0000-0000-000000000001`, `Name = "Demo School"` — see [specs/01-implement-multi-tenant-data-model.md](../../specs/01-implement-multi-tenant-data-model.md).

---

### `Users`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | Surrogate primary key |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | UNIQUE with `Email` | Tenant scope |
| `Email` | `varchar` | 256 | — | NOT NULL | — | UNIQUE with `SchoolId` | Unique per school, not globally |
| `PasswordHash` | `text` | — | — | NOT NULL | — | — | `PasswordHasher<User>` output — never the raw password |
| `DisplayName` | `varchar` | 200 | — | NOT NULL | — | — | — |
| `Role` | `varchar` | 50 | — | NOT NULL | — | — | Stored as string (`Admin`/`Teacher`/`Principal`/`Parent`), not int |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

**Not seeded via migration `HasData`** (unlike `Schools`) — a demo Admin user must never exist in a real deployment, and `HasData` has no concept of environment at migration-apply-time. Instead, seeded at application **startup**, gated by `IsDevelopment()` (`DemoDataSeeder.SeedDemoDataAsync`, called from `Program.cs` after `app.Build()`): `Id = 00000000-0000-0000-0000-000000000002`, `Email = admin@demoschool.test`, tied to the seeded `Schools` row. Idempotent (checked by email before insert) so it's safe to run on every app startup. **Plaintext demo password is documented in this file's Notes section below** (the hash alone isn't reversible — needed for anyone to actually log into the demo). Verified: the `Users` table is empty after a `docker-compose.yml`-only (prod-shaped, `ASPNETCORE_ENVIRONMENT=Production`) run; seeded after a dev (`docker-compose.override.yml`-merged) run.

---

### `RefreshTokens`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | Also used as `ReplacedByTokenId` target |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope — set explicitly by `AuthService`, not auto-stamped (issued pre-authentication) |
| `UserId` | `uuid` | — | — | NOT NULL | FK → `Users.Id` | `ON DELETE CASCADE` | — |
| `TokenHash` | `varchar` | 128 | — | NOT NULL | — | UNIQUE | SHA-256 of the raw refresh token — raw value is never persisted |
| `SessionId` | `uuid` | — | — | NOT NULL | — | indexed | Groups every token issued from one login — revoked as a family on theft detection |
| `ExpiresAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `RevokedAt` | `timestamptz` | — | — | NULL | — | — | Set on rotation, logout, expiry, or theft-family revocation |
| `ReplacedByTokenId` | `uuid` | — | — | NULL | — | — | Points to the token that replaced this one on rotation |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

No seed data — created only through the login/refresh flow.

> No other tables are migrated yet. Schema is provisional and will be filled in as each functionality slice (see [docs/functionality-overview.md](../../docs/functionality-overview.md)) is implemented — don't invent table structure here ahead of actual implementation. Every future table is expected to carry a `SchoolId` column (see Notes below) unless it's explicitly not tenant-scoped.

## Relationships

**`RefreshTokens` → `Users`** (many-to-one, `ON DELETE CASCADE`) — a user's refresh tokens are deleted if the user is deleted. `Users`/`RefreshTokens` relate to `Schools` via `SchoolId` (no FK constraint — enforced at the application/query-filter level per the multi-tenancy design, not the database schema level, same as the rest of the tenant-scoping approach).

## Migration strategy

EF Core Migrations. See [.claude/context/architecture.md § Database migrations](architecture.md#database-migrations) for the full local-dev (`IDesignTimeDbContextFactory`) and deployment (Docker `migrator` stage) flow — not duplicated here to avoid drift between the two files.

## Notes

**`SchoolId` on every table**
Required for the multi-tenant-ready architecture decision (see [.claude/context/architecture.md](architecture.md)) — every table needs this column from its first migration, not retrofitted later, since adding it after the fact would mean an invasive migration across every table and query.

**Demo Admin login credentials**
`admin@demoschool.test` / `Passw0rd!` — the only way to log into the seeded demo, in `Development` only (see `Users` table notes above and `DemoDataSeeder`). See [specs/02-implement-auth.md](../../specs/02-implement-auth.md).
