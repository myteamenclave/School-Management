# Student CRUD

## Problem Statement
How Might We let an Admin register and manage students as first-class records — with enough data to identify, contact, and track enrollment status — without coupling to attendance, grades, or fees?

## Recommended Direction
A student record carries two identifiers: a UUID primary key (globally unique) and a `StudentCode` (human-readable, tenant-scoped). `StudentCode` format is `YYYY-NNNNNN` — enrollment year + 6-digit zero-padded sequential per school. The DB enforces uniqueness on `(SchoolId, StudentCode)`; generation queries MAX for the tenant+year and increments, with a retry loop on the rare conflict.

Student demographic fields: `FirstName`, `LastName`, `DateOfBirth`, `Gender`, `EnrollmentDate`, plus a guardian contact block (`GuardianName`, `GuardianPhone`, `GuardianEmail`) inline on the row. No separate `Guardians` table — the parent-portal auth link (one guardian → multiple children) is a future slice's concern.

Students are never hard-deleted. An `EnrollmentStatus` enum (Active, Transferred, Graduated, Dropped) handles all lifecycle transitions. Only Active students appear in default list queries; status filters expose the rest.

Admin CRUD: Create, Read (list + detail), Update (fields + status), no delete endpoint.

## Key Assumptions to Validate
- [ ] YYYY-NNNNNN (6 digits) is enough for the largest expected school — 999,999 per enrollment year per school
- [ ] Guardian inline columns are sufficient until the parent-portal auth slice arrives
- [ ] No student photo in v1 — confirmed out of scope for the demo
- [ ] `StudentCode` is immutable once created — admin cannot change it
- [ ] `StudentCode` year encodes **enrollment year** (`EnrollmentDate.Year`), not record-creation year

## MVP Scope
- `POST /students` — create with auto-generated `StudentCode`
- `GET /students` — paginated list, default filter Active, optional `status` query param
- `GET /students/{id}` — full detail by UUID
- `PUT /students/{id}` — update demographic fields and/or `EnrollmentStatus`
- `StudentCode` generation: `MAX` query + increment + unique constraint retry on collision
- Soft status transitions via `EnrollmentStatus` enum; no hard delete

## Not Doing (and Why)
- Hard delete — student records must survive for audit/history
- Separate `Guardians` table — deferred to parent-portal auth slice
- Student photo — agreed out of scope for demo v1
- Class/section assignment — deliberate separate slice; keeps this spec clean
- Bulk CSV import — separate slice, comes after single-record CRUD is stable
- Student transfer between schools — multi-tenant concern, not in v1

## Open Questions
*(resolved)*
- `StudentCode` year = enrollment year (`EnrollmentDate.Year`), not record-creation year — a backfilled student who enrolled in 2024 keeps `2024-000001` even when the record is created in 2026.
