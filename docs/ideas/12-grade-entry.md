# Grade Entry — Per Subject / Term

## Problem Statement
How might we let a teacher record each student's performance in a subject for a given semester — as a few fixed component scores that auto-roll into a term grade and letter — so that record feeds the parent portal and admin dashboard, without introducing cross-subject GPA machinery?

## Recommended Direction
One `SubjectTermGrade` record per **student × subject × semester**, holding three nullable component scores — **Midterm, Final, Coursework** — plus an auto-computed `TermScore` and an auto-mapped `LetterGrade`. The rollup is a **fixed school-wide weight constant** (Midterm 30% / Final 40% / Coursework 30%); the letter mapping comes from an **admin-editable `GradeScale`** band table.

Entry is teacher-scoped exactly like attendance: a teacher opens one of their `TeacherSectionSubject` slots (subject + section for the current year), picks a semester (the "term"), sees the enrolled roster, types component scores, and saves via a **bulk upsert** — freely re-editable, latest value wins, no lock. `EnteredByTeacherId` is the audit stamp. Admin gets a **read-only** gradebook view plus CRUD over the `GradeScale`. Every write passes through `AcademicYear.EnsureNotArchived()`.

Semester is the term anchor (the existing two-semester model, `IsCurrent` as the default selection). "No weighted GPA logic" is honored: we weight *within* a subject to compute one term grade — we never aggregate *across* subjects into a GPA.

**Section is provenance, not identity.** The grade is keyed `(SchoolId, StudentId, SubjectId, SemesterId)` — `SectionId` is stored only to record which section context it was entered under. This means:
- A student's overall/average grade is computed by joining on `StudentId` (average of `TermScore`s across their rows for the year) — completely independent of section, so a mid-term section transfer never fragments or double-counts a grade.
- The roster GET lists *currently-enrolled* students for a section and LEFT-JOINs the single grade row by `(student, subject, semester)`. A transferred student's existing grade is reachable by whoever currently teaches their subject — the new teacher continues the same record rather than starting a duplicate.

## Key Assumptions to Validate
- [x] **Term score computes only when all three components are present** (otherwise `TermScore`/letter are null → shown as "In progress"). Renormalizing over entered components is the fallback if this feels wrong in use — revisit after trying the feature.
- [ ] Three fixed components (Midterm/Final/Coursework) fit every subject — no subject needs a different structure.
- [ ] Weights `30/40/30` are the right constant — trivial to change in code, but confirm the split.
- [x] **Grade is keyed to student+subject+semester, not section.** `SectionId` is provenance metadata only; averaging and roster lookup are section-independent, so section transfers are a non-issue.
- [ ] Scores are `0–100` decimals; no extra-credit >100.

## MVP Scope
**New entities:**
- `SubjectTermGrade` — StudentId, SubjectId, SectionId, AcademicYearId, SemesterId, MidtermScore, FinalScore, CourseworkScore (all nullable decimal 0–100), TermScore (computed, nullable), LetterGrade (nullable), EnteredByTeacherId, Notes (nullable). Unique `(SchoolId, StudentId, SubjectId, SemesterId)`. Tenant-scoped.
- `GradeScale` — Letter, MinScore, MaxScore, SchoolId. Seeded with default bands (90+ A, 80+ B, 70+ C, 60+ D, <60 F); admin CRUD.

**Backend API:**
- `GET /api/grades?sectionId=&subjectId=&semesterId=` — roster with each student's components + term + letter (nulls if unentered). Teacher must own that subject-section assignment. Students come from current enrollment; grade rows join by `(student, subject, semester)`.
- `PUT /api/grades/bulk` — upsert full roster: `{ sectionId, subjectId, semesterId, records: [{ studentId, midterm, final, coursework, notes }] }`. Recomputes TermScore + LetterGrade server-side.
- `GET /api/grades?studentId=&academicYearId=` — a student's grades across subjects/terms (parent portal + dashboard).
- `GradeScale` CRUD (Admin only).

**Teacher UI:** Gradebook page — assignment picker (their subject-section slots for the active year) → semester picker → roster table with Midterm/Final/Coursework inputs and read-only Term + Letter columns → Save (bulk upsert, pre-populated if grades exist).

**Admin UI:** Read-only gradebook (grade → section → subject → semester → roster) + `GradeScale` management page.

## Not Doing (and Why)
- **Cross-subject GPA / rank / honor roll** — explicitly out of scope; that's the "weighted GPA logic" the overview excludes.
- **Configurable per-subject weights** — a fixed constant is enough for the demo; a config entity is deferrable.
- **Open/arbitrary assessment list (quizzes, projects)** — full gradebook territory; contradicts "lightweight."
- **Draft→Published lock** — attendance-style free editing was chosen; no lifecycle field.
- **Admin grade entry/override** — teacher-only write path; admin is read-only, corrections go through the teacher (mirrors attendance).
- **Grade change history / audit log** — single current value; a general audit trail is its own backlog item.
- **Report cards / PDF export** — presentation layer, separate feature.
- **Class distribution / average summary in the teacher gradebook** — belongs in the admin dashboard module once grade data exists, not in the entry grid.

## Open Questions (Resolved)
- ~~Partial-entry rollup: null term until complete, or renormalize?~~ **Null until complete**; revisit after using the feature.
- ~~Should the teacher gradebook show a class distribution / average row?~~ **No — dashboard only.**
- ~~When a section transfer happens mid-term, does the grade follow the student or stay?~~ **Neither — the grade is anchored to student+subject+semester, so there is one record reachable regardless of section.** `SectionId` is provenance only.
