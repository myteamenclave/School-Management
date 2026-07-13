# Class / Section Assignment

## Problem Statement

How might we let school admins place students into sections per academic year and
assign teachers to the subjects they teach in specific sections — so that
attendance, gradebook, fee invoicing, and the parent portal all have a consistent,
typed anchor to attach to?

## Recommended Direction

Two independent assignment models, each scoped per academic year:

**Student enrollment** — a single record per student per year:
`StudentSectionEnrollment` (StudentId + SectionId + AcademicYearId). Because
`Section` already carries `GradeId`, grade context is implicit — a student in
section 5-A is automatically in Grade 5 for that year. Admin can update the
`SectionId` on an existing enrollment at any time, covering both intra-grade
transfers (5-A → 5-B) and grade changes (7-A → 6-B or 7-A → 8-A). No history
is kept — the current placement is the source of truth.

The primary UI is a **per-section roster view**: Admin opens a section (e.g.,
5-A) for a selected academic year, sees the enrolled students, adds new students,
and transfers existing ones to a different section.

**Teacher assignment** — a flat many-to-many junction:
`TeacherSectionSubject` (TeacherId + SubjectId + SectionId + AcademicYearId).
One row per teaching slot: "Teacher X teaches Math in 5-A this year." A teacher
can have as many rows as needed (Math in 5-A, Math in 5-B, Physics in 7-A).
Unique constraint on (SubjectId, SectionId, AcademicYearId) — one teacher per
subject per section, no co-teaching.

The primary UI is a **per-teacher assignment view**: Admin opens a teacher,
selects the academic year, and manages their list of subject-section slots for
that year.

## Key Assumptions to Validate

- [x] One placement per student per academic year — updatable, no history needed
      for the demo
- [x] One teacher per subject per section per year (no co-teaching)
- [ ] A student can only be enrolled in one section per year (not split across
      sections for different subjects) — confirm this matches the school's model
- [ ] Enrolling a student requires them to have `EnrollmentStatus = Active` —
      confirm whether inactive/graduated students should be blocked
- [ ] Teacher assignments are per academic year (not persistent across years) —
      Admin re-creates them each year; confirm this is acceptable workload-wise

## MVP Scope

**New entities (backend):**
- `StudentSectionEnrollment` — StudentId, SectionId, AcademicYearId; unique on
  (StudentId, AcademicYearId); tenant-scoped (SchoolId)
- `TeacherSectionSubject` — TeacherId, SubjectId, SectionId, AcademicYearId;
  unique on (SubjectId, SectionId, AcademicYearId); tenant-scoped (SchoolId)

**Backend API flows:**
- `GET /sections/{id}/enrollments?academicYearId=` — list students enrolled in
  a section for a given year (with student name, code, status)
- `POST /sections/{id}/enrollments` — enroll a student (body: StudentId,
  AcademicYearId); returns 409 if student already enrolled for that year
- `PUT /enrollments/{id}` — transfer student to a different section (body: new
  SectionId); validates new section belongs to same school
- `DELETE /enrollments/{id}` — unenroll a student from a section
- `GET /teachers/{id}/assignments?academicYearId=` — list a teacher's
  subject-section assignments for a given year
- `POST /teachers/{id}/assignments` — add a teaching assignment (body: SubjectId,
  SectionId, AcademicYearId); returns 409 if another teacher already covers that
  subject-section-year slot
- `DELETE /teacher-assignments/{id}` — remove a teaching assignment

**Frontend flows:**
- Section Roster page: select academic year → open a section → view enrolled
  students list → enroll a student (search/select from unassigned students) →
  transfer a student to another section
- Teacher Assignment page: open a teacher → select academic year → view
  subject-section slots → add slot (pick subject + section) → remove slot

**Fee invoicing connection:**
`StudentSectionEnrollment` is the query anchor for grade broadcast: given a
target GradeId and AcademicYearId, `JOIN StudentSectionEnrollment → Section`
where `Section.GradeId = targetGradeId` returns all students in that grade for
that year.

## Not Doing (and Why)

- **Homeroom / class teacher designation** — not requested; a simple extension
  (nullable TeacherId on Section) can be added later if needed
- **Transfer history** — Admin updates the placement in place; historical
  placement records add complexity with no value for the demo
- **Bulk enrollment from CSV** — separate bulk import feature in the backlog
- **Timetable / scheduling engine** — teacher-section-subject is the data model;
  timetable (periods, rooms, time slots) is explicitly out of scope for this demo
- **Section capacity limits** — no cap enforcement; add if a real school requires it
- **Subject assignment to sections without a teacher** — a section's subject list
  is implied by the teachers assigned to it; no separate "section curriculum"
  table needed at this stage
- **Cross-year enrollment history report** — out of scope for the demo

## Open Questions

- When enrolling a student in a section, should the system warn (not block) if
  the student's `EnrollmentStatus` is not Active? (Suggested: warn only — Admin
  may legitimately pre-enroll a student who hasn't started yet.)
- Should unenrolling a student from a section be a hard delete or a soft
  "withdrawn" status? (Suggested: hard delete for now — no downstream records
  depend on enrollment yet; revisit when attendance is built.)
- When Admin re-assigns a teacher's slot (changing the teacher on a
  subject-section), should the old row be deleted and a new one created, or is
  an update endpoint needed? (Suggested: delete + create — cleaner audit trail
  and avoids a partial-update edge case.)
