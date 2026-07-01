# Class / Section Structure

## Problem Statement
How might we give school admins a structured way to define the grade and section
hierarchy so that every downstream module (student enrollment, attendance,
gradebook, fees) has a consistent, typed anchor to attach to?

## Recommended Direction
A two-level persistent structure: **Grade** (academic level, e.g. "Grade 5")
contains a configurable number of **Sections** (e.g. "A", "B", "C", "D"). The
number of sections per grade varies — a school might have 4 sections in Grade 1
and 2 in Grade 5. Both entities are tenant-scoped and persistent (not re-created
per academic year). Admin manages the catalog via CRUD; student enrollment will
attach to Section + AcademicYear as a separate concern in a future module.

## Key Assumptions
- [ ] Section names are unique within a grade (two grades can both have "A").
- [ ] Grade deletion is allowed only if the grade has no sections; section deletion
      is allowed freely now (no students yet) — the FK guard is added when Student
      is built.
- [ ] `DisplayOrder` on Grade is admin-set (not auto-inferred from name) to allow
      custom sequencing.
- [ ] No capacity tracking needed in this slice.

## MVP Scope
**In:**
- `Grade` entity: Name, DisplayOrder — Admin CRUD (list, get, create, update, delete)
- `Section` entity: Name, GradeId — Admin CRUD nested under grade
  (list by grade, create, update, delete)
- Both entities are tenant-scoped (SchoolId)
- Grade name unique per school; Section name unique per grade per school
- `DELETE /grades/{id}` returns 400 if the grade still has sections
- FluentValidation on create/update requests
- Integration tests: full CRUD, uniqueness, delete-with-sections guard

**Out:**
- Teacher/homeroom assignment (deferred — Staff CRUD not built yet)
- Per-academic-year section configuration (persistent model chosen)
- Capacity limits on sections
- Subject catalog (future teacher-class-subject module)
- Archiving pattern (grades/sections are structural; hard delete is correct here)

## Not Doing (and Why)
- **Archive instead of delete** — grades/sections are structural catalog entries,
  not time-bounded records. Hard delete + referential integrity is the right model;
  archiving adds complexity with no benefit here.
- **Teacher assignment** — Staff CRUD doesn't exist yet; adding a nullable FK now
  creates a dangling reference risk with no value.
- **Per-year class config** — persistent model is simpler and sufficient; re-
  configuration per year adds setup burden for minimal benefit.
- **Subject catalog** — logically distinct from class structure; belongs with the
  teacher-class-subject assignment module, not here.
- **Cascade delete on Grade** — admin must delete sections first; forces intentional
  cleanup and avoids silent data loss.

## Open Questions
None — all design decisions resolved during ideation.
