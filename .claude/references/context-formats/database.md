<!-- Last verified: [DATE]. Update this line whenever the schema changes. -->

# Database

- Engine: <!-- e.g. PostgreSQL 16 -->
- Schema approach: <!-- e.g. Relational schema with tables: users, orders, products -->

## Schema

Column table format: **Column** | **PG Type** | **Max Length** | **Default** | **Nullable** | **Key** | **Constraints** | **Description**

---

<!-- Add one subsection per table. Copy the block below for each table. -->

### `table_name`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `id` | `UUID` | — | `gen_random_uuid()` | NOT NULL | PK | — | Surrogate primary key |
| `created_at` | `TIMESTAMPTZ` | — | `NOW()` | NOT NULL | — | — | Row creation timestamp |
| `updated_at` | `TIMESTAMPTZ` | — | `NOW()` | NOT NULL | — | — | Last update timestamp; maintained by trigger |
| <!-- column --> | <!-- type --> | <!-- length or — --> | <!-- default or — --> | <!-- NOT NULL or NULL --> | <!-- PK / FK → table.col / — --> | <!-- UNIQUE, CHECK(...), or — --> | <!-- description --> |

<!-- Repeat the table block above for each table. For tables that are planned but not yet migrated, add a note:
> This table is not yet migrated. Schema is provisional and subject to change.
-->

## Relationships

<!-- Describe each relationship as a named pair with cardinality and referential-integrity behavior. Format:

**table_a → table_b** (one-to-many)
Plain-English description of the relationship and what happens on delete (RESTRICT / CASCADE / SET NULL).
-->

## Migration strategy

<!-- Describe how schema changes are applied. Example: "Prisma migrate with staged rollout" or "Flyway, applied by CI on each deploy". -->

## Notes

<!-- Design decisions that are not obvious from the schema and must be understood by anyone writing queries or migrations. Each entry is a bold heading followed by a short explanation. Common things to document:
- Why UUIDs instead of serial integers
- Columns that look like timestamps but serve a different purpose
- Columns that require a DB trigger to stay correct
- Soft-delete approach (or deliberate absence of it)
- Security-motivated storage decisions (e.g. storing a hash instead of the raw value)
- Any type choices made for future-proofing or ease of migration
- Tables that are provisional / not yet migrated
-->
