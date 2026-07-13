# Spec 11 — Implement Class / Section Assignment Frontend

## Related

- **Idea doc:** [docs/ideas/10-class-section-assignment-frontend.md](../docs/ideas/10-class-section-assignment-frontend.md)
- **Backend spec:** [specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md) (implemented)
- **Backend idea:** [docs/ideas/09-class-section-assignment.md](../docs/ideas/09-class-section-assignment.md)

---

## Objective

Add two management surfaces for the class/section assignment data implemented in spec 10:

1. **Section Roster Sheet** — inside the Grades page, each section chip gains a "Roster" button that opens a `Sheet` slide-over with year selection, enrolled student list, and enroll/transfer/remove actions.
2. **Teacher Detail page** — `/admin/teachers/:id` with two tabs: Details (edit form) and Assignments (year-filtered slot list with add/remove).

Plus a small backend addition: a `GET /api/enrollments/enrolled-ids?academicYearId=` endpoint so the enroll student picker can exclude already-placed students.

---

## Part A — Backend: `enrolled-ids` endpoint

No new migration. Three small additions to existing files.

### A1 — `IStudentSectionEnrollmentRepository`

Add one method to `backend/SchoolMgmt.Application/Enrollments/IStudentSectionEnrollmentRepository.cs`:

```csharp
Task<List<Guid>> GetEnrolledStudentIdsForYearAsync(
    Guid academicYearId, CancellationToken ct = default);
```

### A2 — `StudentSectionEnrollmentRepository`

Add implementation to `backend/SchoolMgmt.Infrastructure/Persistence/Repositories/StudentSectionEnrollmentRepository.cs`:

```csharp
public Task<List<Guid>> GetEnrolledStudentIdsForYearAsync(
    Guid academicYearId, CancellationToken ct = default) =>
    _context.Set<StudentSectionEnrollment>()
        .Where(e => e.AcademicYearId == academicYearId)
        .Select(e => e.StudentId)
        .ToListAsync(ct);
```

The global EF Core query filter (`SchoolId = tenantId`) already scopes this to the current tenant — no manual filter needed.

### A3 — `EnrollmentService`

Add one method to `backend/SchoolMgmt.Application/Enrollments/EnrollmentService.cs`:

```csharp
public Task<List<Guid>> GetEnrolledStudentIdsAsync(
    Guid academicYearId, CancellationToken ct = default) =>
    enrollmentRepository.GetEnrolledStudentIdsForYearAsync(academicYearId, ct);
```

### A4 — `EnrollmentsController`

Add one action to `backend/SchoolMgmt.WebApi/Controllers/EnrollmentsController.cs`.

Route `enrolled-ids` is a literal segment and is matched before `{id:guid}`, so there is no ambiguity:

```csharp
[HttpGet("enrolled-ids")]
public async Task<IActionResult> GetEnrolledIds(
    [FromQuery] Guid academicYearId, CancellationToken ct)
{
    var ids = await service.GetEnrolledStudentIdsAsync(academicYearId, ct);
    return Ok(ids);
}
```

---

## Part B — Frontend API clients

### B1 — `frontend/src/api/enrollments.ts` (new file)

```ts
import api from './axios'

export interface EnrollmentDto {
  id: string
  studentId: string
  studentCode: string
  studentFirstName: string
  studentLastName: string
  sectionId: string
  sectionName: string
  gradeId: string
  gradeName: string
  academicYearId: string
  academicYearName: string
  createdAt: string
  updatedAt: string | null
}

export interface CreateEnrollmentRequest {
  studentId: string
  academicYearId: string
}

export interface TransferEnrollmentRequest {
  sectionId: string
}

export const ENROLLMENT_KEYS = {
  bySectionAndYear: (sectionId: string, academicYearId: string) =>
    ['enrollments', 'section', sectionId, academicYearId] as const,
  enrolledIds: (academicYearId: string) =>
    ['enrollments', 'enrolled-ids', academicYearId] as const,
}

export const enrollmentsApi = {
  getBySectionAndYear: (sectionId: string, academicYearId: string) =>
    api
      .get<EnrollmentDto[]>(`/sections/${sectionId}/enrollments`, {
        params: { academicYearId },
      })
      .then((r) => r.data),

  getEnrolledIds: (academicYearId: string) =>
    api
      .get<string[]>('/enrollments/enrolled-ids', { params: { academicYearId } })
      .then((r) => r.data),

  enroll: (sectionId: string, body: CreateEnrollmentRequest) =>
    api
      .post<EnrollmentDto>(`/sections/${sectionId}/enrollments`, body)
      .then((r) => r.data),

  transfer: (id: string, body: TransferEnrollmentRequest) =>
    api.put<EnrollmentDto>(`/enrollments/${id}`, body).then((r) => r.data),

  remove: (id: string) => api.delete(`/enrollments/${id}`),
}
```

### B2 — `frontend/src/api/teacherAssignments.ts` (new file)

```ts
import api from './axios'

export interface TeacherAssignmentDto {
  id: string
  teacherId: string
  subjectId: string
  subjectName: string
  subjectCode: string
  sectionId: string
  sectionName: string
  gradeId: string
  gradeName: string
  academicYearId: string
  academicYearName: string
  createdAt: string
}

export interface CreateTeacherAssignmentRequest {
  subjectId: string
  sectionId: string
  academicYearId: string
}

export const ASSIGNMENT_KEYS = {
  byTeacherAndYear: (teacherId: string, academicYearId: string) =>
    ['assignments', 'teacher', teacherId, academicYearId] as const,
}

export const teacherAssignmentsApi = {
  getByTeacherAndYear: (teacherId: string, academicYearId: string) =>
    api
      .get<TeacherAssignmentDto[]>(`/teachers/${teacherId}/assignments`, {
        params: { academicYearId },
      })
      .then((r) => r.data),

  assign: (teacherId: string, body: CreateTeacherAssignmentRequest) =>
    api
      .post<TeacherAssignmentDto>(`/teachers/${teacherId}/assignments`, body)
      .then((r) => r.data),

  remove: (teacherId: string, assignmentId: string) =>
    api.delete(`/teachers/${teacherId}/assignments/${assignmentId}`),
}
```

---

## Prerequisites

The `Sheet` shadcn component is not yet installed. Before implementing Part C, run:

```
npx shadcn@latest add sheet
```

This creates `frontend/src/components/ui/sheet.tsx`. The sheet primitives used are: `Sheet`, `SheetContent`, `SheetHeader`, `SheetTitle`, `SheetDescription`, `SheetFooter`.

---

## Part C — Section Roster (Grades page)

### C1 — Modify `SectionChip.tsx`

Add an `onRoster` prop. When provided, show a small `Users` icon button to the right of the chip name (outside the click-to-edit zone). The icon is only visible when the user hovers the chip area.

```tsx
interface SectionChipProps {
  section: SectionDto
  gradeId: string
  onRoster?: () => void   // <-- new
}
```

In the non-editing render branch, wrap the chip name and the roster button in a group:

```tsx
<span className="group inline-flex items-center gap-0.5">
  <button
    onClick={() => setEditing(true)}
    className="inline-flex h-7 items-center rounded-full border border-border bg-muted px-3 text-sm text-foreground hover:bg-accent transition-colors"
  >
    {section.name}
  </button>
  {onRoster && (
    <Button
      size="sm"
      variant="ghost"
      className="h-6 w-6 p-0 opacity-0 group-hover:opacity-100 transition-opacity"
      onClick={(e) => { e.stopPropagation(); onRoster() }}
      title="View roster"
    >
      <Users size={13} />
    </Button>
  )}
</span>
```

Import `Users` from `lucide-react`.

### C2 — Modify `GradeAccordionItem.tsx`

Add a `rosterSection` state and pass `onRoster` into each `SectionChip`. Mount `SectionRosterSheet` at the bottom of the component.

```tsx
const [rosterSection, setRosterSection] = useState<SectionDto | null>(null)

// in sections map:
<SectionChip
  key={section.id}
  section={section}
  gradeId={grade.id}
  onRoster={() => setRosterSection(section)}
/>

// after accordion content:
<SectionRosterSheet
  section={rosterSection}
  onClose={() => setRosterSection(null)}
/>
```

### C3 — `SectionRosterSheet.tsx` (new file)

**Location:** `frontend/src/pages/admin/grades/components/SectionRosterSheet.tsx`

**Responsibility:** Year-scoped view of a section's enrollment roster.

**Props:**
```ts
interface SectionRosterSheetProps {
  section: SectionDto | null
  onClose: () => void
}
```

**Behaviour:**
- Uses shadcn/ui `Sheet` (`open={section !== null}`), `SheetContent` (side `"right"`, width ~`w-[480px]`), `SheetHeader`, `SheetTitle`, `SheetDescription`.
- Fetches academic years (`/api/academic-years`) via React Query (reuses `ACADEMIC_YEAR_KEYS.all`).
- Maintains local `selectedYearId` state, defaulting to the first active year (or the first year if none are active).
- Year selector: shadcn/ui `Select` listing all years by name.
- Fetches enrollments via `enrollmentsApi.getBySectionAndYear(section.id, selectedYearId)` — enabled only when both are non-null.
- Table columns: `Code` | `Name` | `Enrolled` (date) | actions (`Transfer` ghost button, `Remove` ghost button).
- "Enroll Student" `Button` in the sheet header area opens `EnrollStudentModal`.
- Transfer row action opens `TransferStudentModal` with that enrollment.
- Remove row action calls `enrollmentsApi.remove(id)` after `window.confirm`, then invalidates `ENROLLMENT_KEYS.bySectionAndYear`.
- Error handling: Sonner toast on failure.
- Loading: spinner row while query is loading.

**React Query keys used:**
- `ACADEMIC_YEAR_KEYS.all` (read-only, already defined in `api/academicYears.ts`)
- `ENROLLMENT_KEYS.bySectionAndYear(section.id, selectedYearId)`

**Invalidation after enroll/transfer/remove:** `ENROLLMENT_KEYS.bySectionAndYear(section.id, selectedYearId)`.

### C4 — `EnrollStudentModal.tsx` (new file)

**Location:** `frontend/src/pages/admin/grades/components/EnrollStudentModal.tsx`

**Props:**
```ts
interface EnrollStudentModalProps {
  open: boolean
  sectionId: string
  academicYearId: string
  onClose: () => void
  onEnrolled: () => void
}
```

**Behaviour:**
- shadcn/ui `Dialog`.
- Fetches `enrollmentsApi.getEnrolledIds(academicYearId)` — the list of student IDs already placed in any section for the year. Enabled while modal is open.
- Debounced search input (300 ms) calling `studentsApi.list({ isActive: true, search, page: 1, pageSize: 20 })`.
- Renders results as a selectable list. Each row shows `StudentCode`, `FirstName LastName`. Rows whose `id` is in the enrolled-ids set are greyed out and non-selectable (already enrolled elsewhere).
- Selecting a student and clicking "Enroll" calls `enrollmentsApi.enroll(sectionId, { studentId, academicYearId })`.
- On 409, show Sonner error toast "Student is already enrolled for this year."
- On success, call `onEnrolled()` then `onClose()`.

**Form:** React Hook Form + Zod (single `studentId` field, `z.string().uuid()`).

### C5 — `TransferStudentModal.tsx` (new file)

**Location:** `frontend/src/pages/admin/grades/components/TransferStudentModal.tsx`

**Props:**
```ts
interface TransferStudentModalProps {
  enrollment: EnrollmentDto | null
  onClose: () => void
  onTransferred: () => void
}
```

**Behaviour:**
- shadcn/ui `Dialog` (`open={enrollment !== null}`).
- Displays current section name read-only.
- Fetches `gradesApi.list()` to build the section list: flat list of all sections across all grades (exclude the current `enrollment.sectionId`). Display each option as `GradeName — SectionName`.
- shadcn/ui `Select` for target section.
- Submit calls `enrollmentsApi.transfer(enrollment.id, { sectionId })`.
- On success, call `onTransferred()` then `onClose()`.

**Form:** React Hook Form + Zod (`sectionId: z.string().uuid()`).

---

## Part D — Teacher Detail page

### D1 — Modify `AdminRoutes` (`frontend/src/pages/admin/index.tsx`)

Add route before or after `teachers`:

```tsx
import { TeacherDetailPage } from './teachers/TeacherDetailPage'

// inside <Routes>:
<Route path="teachers/:id" element={<TeacherDetailPage />} />
```

### D2 — Modify `TeachersPage.tsx`

Two changes only:

1. Import `useNavigate` from `react-router-dom` and `Eye` from `lucide-react`.
2. In each table row's action cell, add a "View" `Button` before the existing "Edit" button:

```tsx
<TableCell>
  <div className="flex items-center gap-1">
    <Button
      size="sm"
      variant="ghost"
      onClick={() => navigate(`/admin/teachers/${teacher.id}`)}
    >
      <Eye size={14} />
    </Button>
    <Button
      size="sm"
      variant="ghost"
      onClick={() => setEditingId(teacher.id)}
    >
      <Pencil size={14} />
    </Button>
  </div>
</TableCell>
```

Widen the action column: `className="w-24"`.

### D3 — `TeacherDetailPage.tsx` (new file)

**Location:** `frontend/src/pages/admin/teachers/TeacherDetailPage.tsx`

**Behaviour:**
- `useParams<{ id: string }>()` for teacher ID.
- Fetches teacher via `teachersApi.getById(id)` (React Query, `TEACHER_KEYS.detail(id)`).
- Back button (`ArrowLeft`) navigating to `/admin/teachers`.
- Page heading: teacher full name + `TeacherCode` monospace badge.
- shadcn/ui `Tabs` with two tabs: **Details** and **Assignments**.

**Details tab:**
- Renders `TeacherDetailsForm` (a new component extracted from `EditTeacherModal` — see below).
- On save, calls `teachersApi.update(id, body)`, shows Sonner success toast, invalidates `TEACHER_KEYS.detail(id)` and `['teachers']`.

**Assignments tab:**
- Renders `AssignmentsTab` (see D5).

**Sub-component: `TeacherDetailsForm`** can be defined inline in the same file or extracted to `components/TeacherDetailsForm.tsx` — prefer inline to avoid extra files.

The form fields match `UpdateTeacherRequest` (firstName, lastName, phone, joiningDate, isActive). Use React Hook Form + Zod, same schema as the existing `EditTeacherModal`. After extracting, `EditTeacherModal` should import and reuse `TeacherDetailsForm` to avoid duplication.

> **Note:** Edit `EditTeacherModal` to delegate the form body to `TeacherDetailsForm` rather than duplicating field definitions. This is the only change to that file.

### D4 — Modify `EditTeacherModal.tsx`

Extract the form fields into `TeacherDetailsForm` (which lives in `TeacherDetailPage.tsx` or a shared `components/` file) and import it. The modal's outer structure (Dialog, header, footer with Save/Cancel buttons) stays unchanged.

### D5 — `AssignmentsTab.tsx` (new file)

**Location:** `frontend/src/pages/admin/teachers/components/AssignmentsTab.tsx`

**Props:**
```ts
interface AssignmentsTabProps {
  teacherId: string
}
```

**Behaviour:**
- Fetches academic years list; maintains `selectedYearId` state (same defaulting logic as `SectionRosterSheet`).
- Year selector: shadcn/ui `Select`.
- Fetches `teacherAssignmentsApi.getByTeacherAndYear(teacherId, selectedYearId)` — enabled when year selected.
- Renders a table or list of assignment rows: `Subject` (name + code badge) | `Grade` | `Section` | actions (Remove).
- "Add Assignment" `Button` opens `AddAssignmentModal`.
- Remove action: calls `teacherAssignmentsApi.remove(teacherId, assignment.id)` after `window.confirm`, invalidates `ASSIGNMENT_KEYS.byTeacherAndYear`.

### D6 — `AddAssignmentModal.tsx` (new file)

**Location:** `frontend/src/pages/admin/teachers/components/AddAssignmentModal.tsx`

**Props:**
```ts
interface AddAssignmentModalProps {
  open: boolean
  teacherId: string
  academicYearId: string
  onClose: () => void
  onAssigned: () => void
}
```

**Behaviour:**
- shadcn/ui `Dialog`.
- Three fields, all `Select`:
  - **Grade** — fetches `gradesApi.list()`, selecting a grade populates the Section select.
  - **Section** — sections from the selected grade.
  - **Subject** — fetches `subjectsApi.list({ isActive: true, page: 1, pageSize: 100 })` (all active subjects). The subjects list is fetched once on modal open, not dependent on grade.
- Submit calls `teacherAssignmentsApi.assign(teacherId, { subjectId, sectionId, academicYearId })`.
- On 409, show Sonner error "A teacher is already assigned to this subject in this section for this year."
- On success, call `onAssigned()` then `onClose()`.

**Form:** React Hook Form + Zod:
```ts
z.object({
  gradeId: z.string().uuid(),
  sectionId: z.string().uuid(),
  subjectId: z.string().uuid(),
})
```
`gradeId` is only used for filtering the section dropdown — it is not sent to the API.

---

## Project Structure Changes

### New files
```
frontend/src/api/
  enrollments.ts
  teacherAssignments.ts

frontend/src/pages/admin/grades/components/
  SectionRosterSheet.tsx
  EnrollStudentModal.tsx
  TransferStudentModal.tsx

frontend/src/pages/admin/teachers/
  TeacherDetailPage.tsx
  components/
    AssignmentsTab.tsx
    AddAssignmentModal.tsx
```

### Modified files
```
backend/SchoolMgmt.Application/Enrollments/
  IStudentSectionEnrollmentRepository.cs   (+ 1 method)
  EnrollmentService.cs                     (+ 1 method)

backend/SchoolMgmt.Infrastructure/Persistence/Repositories/
  StudentSectionEnrollmentRepository.cs    (+ 1 method)

backend/SchoolMgmt.WebApi/Controllers/
  EnrollmentsController.cs                 (+ 1 GET action)

frontend/src/pages/admin/
  index.tsx                                (+ teachers/:id route)
  grades/components/
    SectionChip.tsx                        (+ onRoster prop)
    GradeAccordionItem.tsx                 (+ rosterSection state + Sheet mount)
  teachers/
    TeachersPage.tsx                       (+ View button)
    components/EditTeacherModal.tsx        (delegate form to TeacherDetailsForm)
```

---

## Academic Year API dependency

Both surfaces require the academic years list. Use the existing `ACADEMIC_YEAR_KEYS` and `academicYearsApi.list()` from `frontend/src/api/academicYears.ts`. Default the year dropdown to the first year where `isArchived === false`, falling back to `years[0]` if all are archived.

---

## Success Criteria

### Backend
- `GET /api/enrollments/enrolled-ids?academicYearId=<guid>` returns `200 []` for an empty year and `200 [<studentId>]` after enrolling one student.
- Unauthenticated request returns `401`; non-Admin returns `403`.

### Frontend — Section Roster
- Hovering a section chip reveals the `Users` icon; clicking it opens the Sheet.
- Selecting a year in the Sheet loads that year's enrollment list.
- "Enroll Student" modal shows only active students not yet in any section for the year.
- Enrolling adds the student to the roster table; transfer moves them; remove removes them — all without a page reload.
- 409 from the enroll endpoint shows an error toast; the modal stays open.

### Frontend — Teacher Detail
- Clicking the Eye icon on a teacher row navigates to `/admin/teachers/:id`.
- The Details tab shows the teacher's current data and saves edits correctly.
- The Assignments tab shows slots for the selected year.
- "Add Assignment" lets the admin pick grade → section → subject and confirms the new slot appears in the list.
- Remove assignment works; 409 (duplicate) shows an error toast.
- Back button returns to the Teachers list.
- The existing Pencil quick-edit modal on the list page still works.

---

## Out of Scope

- Pagination inside the Section Roster Sheet.
- Enrollment history / audit log.
- Teacher assignment from the section perspective.
- Student detail page showing their enrolled section.
- Any routing changes beyond `teachers/:id`.
