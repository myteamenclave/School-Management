# Attendance Marking

## Problem Statement

How might we let teachers record a single daily attendance status (Present / Late / Absent / Excused) for each student in their section, so the school has an accurate roll-call record that feeds the parent portal and the admin dashboard?

## Recommended Direction

One `AttendanceRecord` per student per section per date. A teacher sees all sections they teach (via their `TeacherSectionSubject` assignments), picks a section and a date, and sees the full student roster with a status dropdown for each. They submit the whole day at once — a bulk upsert endpoint replaces individual per-student calls. The `MarkedByTeacherId` field provides an audit trail of who submitted.

Admin gets a read-only view of any section's attendance for any date. No editing from the admin side — corrections go through the teacher.

Attendance is scoped per-section, not per-subject. Any teacher assigned to a subject in a section can mark daily roll call for that section. The unique constraint `(SchoolId, StudentId, SectionId, Date)` prevents double-marking regardless of which teacher submits.

## Key Assumptions

- [x] One attendance status per student per section per date (not per subject)
- [x] Any teacher assigned to a subject in a section can mark roll call for that section
- [x] Bulk upsert per section+date — teacher submits the whole roster at once; re-submitting corrects the record
- [x] Teachers can mark attendance for any date (past or future) — no date range restriction
- [x] Admin view is read-only — corrections go through the teacher

## MVP Scope

**New entity:**
- `AttendanceRecord` — StudentId, SectionId, AcademicYearId, Date (DateOnly), Status (enum: Present/Late/Absent/Excused), MarkedByTeacherId, Notes (nullable, varchar 500)
- Unique constraint: (SchoolId, StudentId, SectionId, Date)
- Tenant-scoped (SchoolId on every row)

**Backend API:**
- `GET /api/attendance?sectionId=&date=` — returns the full student roster for that section with their current status for that date (null if not yet marked)
- `PUT /api/attendance/bulk` — upsert a full day's roll call; body: `{ sectionId, academicYearId, date, records: [{ studentId, status, notes }] }`
- `GET /api/attendance?studentId=&academicYearId=` — a student's full attendance history for a year (for parent portal and dashboard)

**Teacher UI:**
- Attendance page in the Teacher section of the app shell
- Section picker (only shows the teacher's assigned sections for the active year)
- Date picker (any date)
- Roster table: one row per enrolled student, status dropdown (Present / Late / Absent / Excused), optional notes field
- Submit button — bulk upsert; success toast
- If attendance already exists for the selected section+date, pre-populate the form

**Admin UI:**
- Read-only attendance view: grade → section → date → roster with statuses
- Accessible from the Grades/Sections area or a dedicated Attendance nav item

## Not Doing (and Why)

- **Per-subject (period-level) attendance** — user chose daily roll call; per-subject adds data entry overhead with limited extra value for the demo
- **Attendance reports / analytics** — that belongs in the dashboard module once data exists
- **Parent notifications on absence** — out of scope for this build; parent portal is a separate module
- **Automated thresholds** (e.g. flag after 5 absences) — future feature, not MVP
- **Homeroom teacher designation** — not in the current data model; any subject teacher can mark roll for their section
- **Bulk import of historical attendance** — separate bulk import feature in the backlog
- **Date range enforcement** — system will warn if marking outside the active academic year's date range but will not block

## Open Questions (Resolved)

- Should teachers be blocked from marking attendance outside the active academic year's date range? → **Warn only, don't block.**
- Should Admin be able to edit attendance, or only view? → **View only.**
