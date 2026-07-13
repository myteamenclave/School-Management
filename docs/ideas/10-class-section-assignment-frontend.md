# Class / Section Assignment — Frontend

## Problem Statement

How might we let school admins place students into section rosters and manage
teacher subject-section slots — fitting naturally into the existing Grades and
Teachers admin flows, without introducing new top-level navigation?

## Recommended Direction

Two embedded surfaces, zero new nav entries.

**Surface 1 — Section Roster (inside Grades page):**
Each section row in the Grades page gets a "Roster" button. Clicking it opens a
`Sheet` slide-over from the right. The sheet shows an academic year dropdown, a
table of enrolled students (Code, Name, Transfer, Remove), and an "Enroll
Student" button. Enrolling opens a small modal with a debounced searchable
student picker that excludes students already enrolled in any section for the
selected year. Transfer opens a modal with a section selector.

**Surface 2 — Teacher Detail page (`/admin/teachers/:teacherId`):**
The Teachers list table gains a "View" button alongside the existing "Edit"
quick-modal button. "View" navigates to a detail page with two tabs: **Details**
(the current editable form) and **Assignments** (academic year dropdown + list of
subject-section slots + Add Slot / Remove actions).

Both surfaces follow the established stack: React Query, React Hook Form + Zod,
shadcn/ui `Sheet` / `Dialog` / `Select`, Sonner toasts.

## Key Assumptions to Validate

- [ ] The Grades page already renders individual section rows that can accept a
      click/button handler — check `GradesPage.tsx` before starting.
- [ ] The existing `EditTeacherModal` form fields can be extracted cleanly into a
      `TeacherDetailPage` Details tab without breaking the list page.
- [ ] Section rosters stay small enough (< ~50 students) that no pagination is
      needed inside the Sheet.
- [ ] A debounced search returning the first 20 matching active students is
      sufficient UX for the enroll picker.

## MVP Scope

**Backend addition (no new migration):**
- `GET /api/enrollments/enrolled-ids?academicYearId=` — returns `Guid[]` of
  student IDs already enrolled in any section for that year. Used by the enroll
  picker to filter out already-placed students.

**New frontend files:**
- `src/api/enrollments.ts` — DTOs + query key factory + `enrollmentsApi`
- `src/api/teacherAssignments.ts` — DTOs + query key factory + `teacherAssignmentsApi`
- `src/pages/admin/grades/components/SectionRosterSheet.tsx`
- `src/pages/admin/grades/components/EnrollStudentModal.tsx`
- `src/pages/admin/grades/components/TransferStudentModal.tsx`
- `src/pages/admin/teachers/TeacherDetailPage.tsx`
- `src/pages/admin/teachers/components/AssignmentsTab.tsx`
- `src/pages/admin/teachers/components/AddAssignmentModal.tsx`

**Modified files:**
- `src/pages/admin/grades/GradesPage.tsx` — add "Roster" button per section row
- `src/pages/admin/teachers/TeachersPage.tsx` — add "View" button, keep "Edit"
  quick-modal
- `src/pages/admin/index.tsx` — add `teachers/:id` route

## Not Doing (and Why)

- **Global academic year selector in AppShell** — adds shared state complexity
  for no clear benefit; per-page dropdowns are simpler and sufficient.
- **Enrollment tab on Student profile** — Admin thinks "which students are in
  this section?", not "which section is this student in?"; section-first is the
  right mental model.
- **Assignments from the section perspective (section shows its teachers)** — a
  third surface the demo doesn't need; teacher-centric view is sufficient.
- **Paginated roster inside the Sheet** — sections are small at demo scale;
  pagination in a slide-over adds friction for no gain.
- **Transfer history / enrollment log** — hard delete per backend spec; no
  history to show.
- **Inactive student / teacher guard UI** — backend handles via 409 / service
  logic; no frontend guard needed for the demo.
