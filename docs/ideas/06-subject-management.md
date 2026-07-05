# Subject Management

## Problem Statement
How might we give school admins a minimal, reliable subject catalog that downstream features (grade entry, teacher-subject assignment) can safely reference without coupling to academic years, sections, or teacher assignments?

## Recommended Direction
A flat, school-scoped `Subject` entity: `Name`, a manually-entered `Code` (short, unique per school), an optional `Description`, and an `IsActive` flag. Admin CRUD only. Teachers read from this catalog when entering grades (future spec). No academic-year coupling — subjects are stable school-level data; an `IsActive` flag handles discontinued subjects without versioning overhead.

Subject codes are human-chosen and human-readable (e.g. `MATH`, `SCI`, `ENG`). Uniqueness is enforced per school at the DB level. Codes are immutable once assigned to keep grade and assignment records stable.

## Key Assumptions to Validate
- [x] Subjects are stable across academic years — confirmed acceptable for demo scope
- [x] Subject codes are manually entered (short, human-readable), not auto-generated
- [ ] One subject can be taught by multiple teachers in different sections — the data model must not enforce 1:1 (deferred to the assignment spec)
- [ ] No subject hierarchy (e.g. Science > Biology) — flat catalog is correct for this scope

## MVP Scope
- `POST /api/subjects` — create with manually entered `Code`; unique index enforces no duplicates per school
- `GET /api/subjects` — paginated list; default active-only; optional `isActive` filter and `search` query params (ILIKE on `Name` and `Code`)
- `GET /api/subjects/{id}` — full detail
- `PUT /api/subjects/{id}` — update `Name`, `Description`, `IsActive`; `Code` is immutable
- No hard delete — `IsActive = false` retires a subject

## Not Doing (and Why)
- Academic-year scoping — adds a join to every downstream reference; overkill for demo
- Grade-level hints (`GradeLevelHint` metadata on subjects) — deferred to the teacher-assignment spec where it's actually consumed
- Department/category grouping (`SubjectCategory` entity) — adds a second entity to manage; not needed for the catalog-only scope
- Teacher-subject assignment — explicit separate spec; this one only builds the catalog
- Auto-generated codes — opaque codes are less useful than human-chosen ones when teachers reference subjects by code in grade entry
- Hard delete — subjects must survive for historical grade records

## Open Questions
*(resolved)*
- Code uniqueness scope: per school (not global) — two schools can both define `MATH`; the `(SchoolId, Code)` unique index enforces this
- Code immutability: immutable once created, same rationale as `StudentCode`/`TeacherCode`
