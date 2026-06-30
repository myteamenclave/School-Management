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

> No other tables are migrated yet. Schema is provisional and will be filled in as each functionality slice (see [docs/functionality-overview.md](../../docs/functionality-overview.md)) is implemented — don't invent table structure here ahead of actual implementation. Every future table is expected to carry a `SchoolId` column (see Notes below) unless it's explicitly not tenant-scoped, same as `Schools` itself isn't.

## Relationships

None yet — `Schools` is the only table. Future tenant-scoped tables relate to it via `SchoolId` (no FK constraint is planned; enforced at the application/query-filter level per the multi-tenancy design, not the database schema level).

## Migration strategy

EF Core Migrations. See [.claude/context/architecture.md § Database migrations](architecture.md#database-migrations) for the full local-dev (`IDesignTimeDbContextFactory`) and deployment (Docker `migrator` stage) flow — not duplicated here to avoid drift between the two files.

## Notes

**`SchoolId` on every table**
Required for the multi-tenant-ready architecture decision (see [.claude/context/architecture.md](architecture.md)) — every table needs this column from its first migration, not retrofitted later, since adding it after the fact would mean an invasive migration across every table and query.
