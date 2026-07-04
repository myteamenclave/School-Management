# Spec: Implement Student CRUD — Frontend

## Related docs & specs

- [docs/ideas/05-student-crud-frontend.md](../docs/ideas/05-student-crud-frontend.md) — UX decisions: paginated table, status tabs, debounced server-side search, modal create/edit, Prev/Next pagination
- [docs/ideas/03-student-crud.md](../docs/ideas/03-student-crud.md) — domain rationale: StudentCode immutability, EnrollmentStatus lifecycle, no hard delete, guardian inline
- [specs/05-implement-student-crud.md](05-implement-student-crud.md) — backend spec: API routes, DTOs, error responses, PagedResult shape, EnrollmentStatus enum values
- [specs/05-B-implement-class-section-frontend.md](05-B-implement-class-section-frontend.md) — closest frontend pattern: API client shape, modal structure, React Query + RHF + Zod conventions, testing approach
- [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md) — frontend foundation: Axios instance, TanStack Query, React Hook Form + Zod, AppShell + NAV_ITEMS, RoleRoute

## Objective

Build the Admin-only Students page (`/admin/students`) that lets school admins browse the student roster, register new students, and edit existing student records (including enrollment status transitions). The page is a server-paginated data table filtered by enrollment status tabs and a debounced name/code search. Create and Edit each open a modal dialog — consistent with the Academic Years and Grades pages.

This spec also extends the backend `GET /api/students` endpoint with a `?search=` query parameter (ILIKE filter on `FirstName`, `LastName`, and `StudentCode`). That is the only backend change; no new migrations are needed.

**Out of scope:** student detail page, bulk CSV import, class/section assignment, student photo, export, any role other than Admin.

## Tech Stack

Same as [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md). Three additional shadcn/ui components are needed:

```bash
# Run from frontend/
npx shadcn@latest add table select tabs
```

| Component | Used for |
|---|---|
| `Table` | Paginated student roster |
| `Select` | Gender and EnrollmentStatus dropdowns in modals |
| `Tabs` | Status filter bar (Active / Transferred / Graduated / Dropped) |

## Design

---

## Part 1 — Backend Addition: `?search=` query parameter

This is a surgical, no-migration change to four existing files.

### `IStudentRepository` — add `search` parameter

```csharp
// SchoolMgmt.Application/Students/IStudentRepository.cs
Task<(List<Student> Items, int TotalCount)> GetPagedAsync(
    EnrollmentStatus? status, string? search, int page, int pageSize, CancellationToken ct = default);
```

### `StudentRepository` — ILIKE filter

```csharp
public async Task<(List<Student> Items, int TotalCount)> GetPagedAsync(
    EnrollmentStatus? status, string? search, int page, int pageSize, CancellationToken ct = default)
{
    var query = DbSet.AsQueryable();

    if (status.HasValue)
        query = query.Where(s => s.EnrollmentStatus == status.Value);
    else
        query = query.Where(s => s.EnrollmentStatus == EnrollmentStatus.Active);

    if (!string.IsNullOrWhiteSpace(search))
    {
        var pattern = $"%{search.Trim()}%";
        query = query.Where(s =>
            EF.Functions.ILike(s.FirstName + " " + s.LastName, pattern) ||
            EF.Functions.ILike(s.StudentCode, pattern));
    }

    var total = await query.CountAsync(ct);
    var items = await query
        .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

    return (items, total);
}
```

`EF.Functions.ILike` is available via `Npgsql.EntityFrameworkCore.PostgreSQL` (already in the project). Case-insensitive search across full name and student code — no DB index change required for a demo-scale build.

### `StudentService` — thread `search` through

```csharp
public async Task<PagedResult<StudentSummaryDto>> GetStudentsAsync(
    EnrollmentStatus? status, string? search, int page, int pageSize, CancellationToken ct)
{
    var (items, total) = await _students.GetPagedAsync(status, search, page, pageSize, ct);
    return new PagedResult<StudentSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize);
}
```

### `StudentsController` — expose `?search=` query param

```csharp
[HttpGet]
public async Task<IActionResult> GetStudents(
    [FromQuery] string? status = null,
    [FromQuery] string? search = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    pageSize = Math.Min(pageSize, 100);
    EnrollmentStatus? parsedStatus = null;
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<EnrollmentStatus>(status, true, out var s))
        parsedStatus = s;

    var result = await _studentService.GetStudentsAsync(parsedStatus, search, page, pageSize, ct);
    return Ok(result);
}
```

The `search` value is passed directly to the service; the ILIKE pattern construction lives in the repository. No FluentValidation change needed — `search` has no structural constraint on the API boundary.

---

## Part 2 — Frontend

### API client — `src/api/students.ts`

DTOs mirror the backend response shapes exactly. `DateOnly` fields arrive as `"YYYY-MM-DD"` strings from the JSON serializer.

```ts
import api from './axios'

export interface StudentSummaryDto {
  id: string
  studentCode: string
  firstName: string
  lastName: string
  dateOfBirth: string        // "YYYY-MM-DD"
  gender: string             // "Male" | "Female" | "Other"
  enrollmentDate: string     // "YYYY-MM-DD"
  enrollmentStatus: string   // "Active" | "Transferred" | "Graduated" | "Dropped"
}

export interface StudentDto extends StudentSummaryDto {
  guardianName: string | null
  guardianPhone: string | null
  guardianEmail: string | null
  createdAt: string
  updatedAt: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CreateStudentRequest {
  firstName: string
  lastName: string
  dateOfBirth: string        // "YYYY-MM-DD"
  gender: string
  enrollmentDate: string     // "YYYY-MM-DD"
  guardianName?: string
  guardianPhone?: string
  guardianEmail?: string
}

export interface UpdateStudentRequest extends CreateStudentRequest {
  enrollmentStatus: string
}

export interface ListStudentsParams {
  status: string
  search: string
  page: number
  pageSize: number
}

export const STUDENT_KEYS = {
  list: (p: ListStudentsParams) => ['students', 'list', p] as const,
  detail: (id: string) => ['students', 'detail', id] as const,
}

export const studentsApi = {
  list: (params: ListStudentsParams) =>
    api.get<PagedResult<StudentSummaryDto>>('/students', { params }).then((r) => r.data),

  getById: (id: string) =>
    api.get<StudentDto>(`/students/${id}`).then((r) => r.data),

  create: (body: CreateStudentRequest) =>
    api.post<StudentDto>('/students', body).then((r) => r.data),

  update: (id: string, body: UpdateStudentRequest) =>
    api.put<StudentDto>(`/students/${id}`, body).then((r) => r.data),
}
```

`PagedResult<T>` is defined here rather than in a shared types file because it mirrors the backend DTO and is only consumed by the students feature for now. Future feature slices that return paginated results should move it to `src/types/api.ts` at that point.

### Error extraction helper

Defined once at the top of `StudentsPage.tsx`, passed as a prop to modals:

```ts
function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}
```

### Status tab type

```ts
type EnrollmentTab = 'Active' | 'Transferred' | 'Graduated' | 'Dropped'
const ENROLLMENT_TABS: EnrollmentTab[] = ['Active', 'Transferred', 'Graduated', 'Dropped']
```

### Page component — `src/pages/admin/students/StudentsPage.tsx`

**State:**
- `tab: EnrollmentTab` — default `'Active'`; drives `?status=` param
- `search: string` — raw controlled input value
- `debouncedSearch: string` — 300 ms debounced value; drives `?search=` param
- `page: number` — default `1`; reset to `1` whenever `tab` or `debouncedSearch` changes
- `createOpen: boolean` — controls `CreateStudentModal`
- `editingId: string | null` — non-null when `EditStudentModal` is open

**Debounce:**
```ts
const [search, setSearch] = useState('')
const [debouncedSearch, setDebouncedSearch] = useState('')

useEffect(() => {
  const t = setTimeout(() => {
    setDebouncedSearch(search)
    setPage(1)
  }, 300)
  return () => clearTimeout(t)
}, [search])
```

Reset page on tab change:
```ts
const handleTabChange = (value: string) => {
  setTab(value as EnrollmentTab)
  setPage(1)
}
```

**Data fetching:**
```ts
const queryParams: ListStudentsParams = {
  status: tab,
  search: debouncedSearch,
  page,
  pageSize: 20,
}

const { data, isLoading, isError } = useQuery({
  queryKey: STUDENT_KEYS.list(queryParams),
  queryFn: () => studentsApi.list(queryParams),
  placeholderData: keepPreviousData,  // avoids flash between pages
})
```

`keepPreviousData` keeps the previous page visible while the next page loads, preventing layout shift on pagination.

**Derived values:**
```ts
const totalPages = data ? Math.ceil(data.totalCount / 20) : 0
```

**Layout:**
```tsx
<div className="px-8 py-8 max-w-6xl mx-auto">
  {/* Page header */}
  <div className="flex items-start justify-between mb-6">
    <div>
      <h1 className="font-heading text-2xl font-semibold text-foreground">Students</h1>
      <p className="text-sm text-muted-foreground mt-1">
        Manage student records and enrollment status.
      </p>
    </div>
    <Button onClick={() => setCreateOpen(true)}>
      <Plus size={16} className="mr-2" /> Add Student
    </Button>
  </div>

  {/* Filters row: status tabs + search */}
  <div className="flex items-center justify-between gap-4 mb-4">
    <Tabs value={tab} onValueChange={handleTabChange}>
      <TabsList>
        {ENROLLMENT_TABS.map((t) => (
          <TabsTrigger key={t} value={t}>{t}</TabsTrigger>
        ))}
      </TabsList>
    </Tabs>

    <div className="relative w-64">
      <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
      <Input
        placeholder="Search name or code…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="pl-9"
      />
    </div>
  </div>

  {/* Table */}
  {isLoading && (
    <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
      Loading…
    </div>
  )}
  {isError && (
    <div className="flex items-center justify-center h-48 text-sm text-destructive">
      Failed to load students.
    </div>
  )}
  {!isLoading && !isError && (
    <>
      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Gender</TableHead>
              <TableHead>Enrolled</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-16" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="h-32 text-center text-muted-foreground text-sm">
                  No students found.
                </TableCell>
              </TableRow>
            ) : (
              data?.items.map((student) => (
                <TableRow key={student.id}>
                  <TableCell>
                    <span className="font-mono text-xs text-muted-foreground">
                      {student.studentCode}
                    </span>
                  </TableCell>
                  <TableCell className="font-medium">
                    {student.firstName} {student.lastName}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {student.gender}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {student.enrollmentDate}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={student.enrollmentStatus} />
                  </TableCell>
                  <TableCell>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => setEditingId(student.id)}
                    >
                      <Pencil size={14} />
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-3 mt-4">
          <Button
            size="sm"
            variant="outline"
            disabled={page === 1}
            onClick={() => setPage((p) => p - 1)}
          >
            <ChevronLeft size={15} className="mr-1" /> Prev
          </Button>
          <span className="text-sm text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          <Button
            size="sm"
            variant="outline"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Next <ChevronRight size={15} className="ml-1" />
          </Button>
        </div>
      )}
    </>
  )}

  <CreateStudentModal
    open={createOpen}
    onClose={() => setCreateOpen(false)}
    onCreated={() => queryClient.invalidateQueries({ queryKey: ['students'] })}
  />
  <EditStudentModal
    studentId={editingId}
    onClose={() => setEditingId(null)}
    onUpdated={() => queryClient.invalidateQueries({ queryKey: ['students'] })}
  />
</div>
```

**`StatusBadge` helper (inline in `StudentsPage.tsx`):**

```tsx
const STATUS_STYLES: Record<string, string> = {
  Active:      'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400',
  Transferred: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
  Graduated:   'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400',
  Dropped:     'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
}

function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_STYLES[status] ?? 'bg-muted text-muted-foreground'}`}>
      {status}
    </span>
  )
}
```

Defined inline in the page file rather than extracted — `StatusBadge` is only used in this file. Extract to `components/` only if a second consumer appears.

### Create student modal — `src/pages/admin/students/components/CreateStudentModal.tsx`

**Props:**
```ts
interface CreateStudentModalProps {
  open: boolean
  onClose: () => void
  onCreated: () => void
}
```

**Zod schema:**
```ts
const schema = z.object({
  firstName:     z.string().min(1, 'Required').max(100),
  lastName:      z.string().min(1, 'Required').max(100),
  dateOfBirth:   z.string().min(1, 'Required'),
  gender:        z.enum(['Male', 'Female', 'Other'], { required_error: 'Required' }),
  enrollmentDate: z.string().min(1, 'Required'),
  guardianName:  z.string().max(200).optional(),
  guardianPhone: z.string().max(20).optional(),
  guardianEmail: z.string().email('Invalid email').max(256).optional().or(z.literal('')),
})
type FormValues = z.infer<typeof schema>
```

**Form layout** (inside `<Dialog>`):

```
Row 1: First Name | Last Name          (grid-cols-2)
Row 2: Date of Birth | Gender          (grid-cols-2)
Row 3: Enrollment Date                 (full width)
Divider: "Guardian (optional)"
Row 4: Guardian Name                   (full width)
Row 5: Guardian Phone | Guardian Email (grid-cols-2)
```

**Gender field** — use the shadcn `Select` component (not a native `<select>`):

```tsx
<Controller
  name="gender"
  control={control}
  render={({ field }) => (
    <Select onValueChange={field.onChange} value={field.value}>
      <SelectTrigger>
        <SelectValue placeholder="Select gender" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="Male">Male</SelectItem>
        <SelectItem value="Female">Female</SelectItem>
        <SelectItem value="Other">Other</SelectItem>
      </SelectContent>
    </Select>
  )}
/>
```

Use the same `Controller` + shadcn `Select` pattern for `EnrollmentStatus` in the edit modal.

**Date fields** — native `<input type="date">` styled to match the existing date inputs in `CreateYearModal.tsx`:
```
className="flex h-11 w-full rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
```

**Mutation:**
```ts
const mutation = useMutation({
  mutationFn: studentsApi.create,
  onSuccess: () => {
    toast.success('Student created')
    reset()
    onCreated()
    onClose()
  },
  onError: (err) => toast.error(extractError(err)),
})
```

No 409 handling for create — `StudentCode` collision is resolved by the backend retry loop transparently. Generic `extractError` covers the rare `DomainException` ("Unable to assign a student code") that surfaces only after 3 consecutive collisions.

**Payload mapping** — strip empty optional strings before sending:
```ts
const onSubmit = (data: FormValues) => mutation.mutate({
  ...data,
  guardianName:  data.guardianName  || undefined,
  guardianPhone: data.guardianPhone || undefined,
  guardianEmail: data.guardianEmail || undefined,
})
```

**Submit button:** `isSubmitting ? 'Creating…' : 'Create Student'`

**Dialog max width:** `sm:max-w-lg` (wider than academic years modal to accommodate the two-column layout).

### Edit student modal — `src/pages/admin/students/components/EditStudentModal.tsx`

The edit modal fetches the full `StudentDto` (including guardian fields) by ID — the list only returns `StudentSummaryDto`.

**Props:**
```ts
interface EditStudentModalProps {
  studentId: string | null   // null = closed
  onClose: () => void
  onUpdated: () => void
}
```

`open` is derived: `studentId !== null`.

**Data fetch:**
```ts
const { data: student, isLoading } = useQuery({
  queryKey: STUDENT_KEYS.detail(studentId ?? ''),
  queryFn: () => studentsApi.getById(studentId!),
  enabled: studentId !== null,
})
```

**Zod schema** — extends the create schema with `enrollmentStatus`:
```ts
const schema = createSchema.extend({
  enrollmentStatus: z.enum(['Active', 'Transferred', 'Graduated', 'Dropped'], {
    required_error: 'Required',
  }),
})
type FormValues = z.infer<typeof schema>
```

**Form population** — use `useEffect` on `student`:
```ts
useEffect(() => {
  if (student) {
    reset({
      firstName:        student.firstName,
      lastName:         student.lastName,
      dateOfBirth:      student.dateOfBirth,
      gender:           student.gender,
      enrollmentDate:   student.enrollmentDate,
      enrollmentStatus: student.enrollmentStatus,
      guardianName:     student.guardianName  ?? '',
      guardianPhone:    student.guardianPhone ?? '',
      guardianEmail:    student.guardianEmail ?? '',
    })
  }
}, [student, reset])
```

**Modal header** — show StudentCode as a read-only badge above the form fields:
```tsx
<DialogHeader>
  <DialogTitle>Edit Student</DialogTitle>
  {student && (
    <p className="text-xs text-muted-foreground font-mono mt-0.5">
      {student.studentCode}
    </p>
  )}
</DialogHeader>
```

`StudentCode` is **never** a form field. It is display-only and must never appear in the `UpdateStudentRequest` payload.

**Form layout** — same two-column grid as create, with `EnrollmentStatus` added as a full-width row between Enrollment Date and the guardian divider:

```
Row 1: First Name | Last Name
Row 2: Date of Birth | Gender
Row 3: Enrollment Date
Row 4: Enrollment Status          ← new in edit; not in create
Divider: "Guardian (optional)"
Row 5: Guardian Name
Row 6: Guardian Phone | Guardian Email
```

**Loading state** — while `isLoading` is true (first open before data arrives), render the modal shell with a centered spinner inside the form area instead of the form fields. Do not block the modal from opening.

**Mutation:**
```ts
const mutation = useMutation({
  mutationFn: (data: FormValues) => studentsApi.update(studentId!, {
    firstName:        data.firstName,
    lastName:         data.lastName,
    dateOfBirth:      data.dateOfBirth,
    gender:           data.gender,
    enrollmentDate:   data.enrollmentDate,
    enrollmentStatus: data.enrollmentStatus,
    guardianName:     data.guardianName  || undefined,
    guardianPhone:    data.guardianPhone || undefined,
    guardianEmail:    data.guardianEmail || undefined,
  }),
  onSuccess: () => {
    toast.success('Student updated')
    onUpdated()
    onClose()
  },
  onError: (err) => toast.error(extractError(err)),
})
```

**Submit button:** `isSubmitting ? 'Saving…' : 'Save Changes'`

### Route wiring — `src/pages/admin/index.tsx`

```tsx
import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage } from './academic-years/AcademicYearsPage'
import { GradesPage } from './grades/GradesPage'
import { StudentsPage } from './students/StudentsPage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years" element={<AcademicYearsPage />} />
      <Route path="grades" element={<GradesPage />} />
      <Route path="students" element={<StudentsPage />} />
    </Routes>
  )
}
```

### Sidebar nav — `src/layouts/AppShell.tsx`

Add a `Students` entry to `NAV_ITEMS`. Import `GraduationCap` from `lucide-react`:

```ts
{
  label: 'Students',
  to: '/admin/students',
  icon: <GraduationCap size={18} />,
  roles: ['Admin'],
},
```

Position it after Grades & Sections in the array.

---

## Project Structure

New and modified files introduced by this spec:

```
backend/
  SchoolMgmt.Application/
    Students/
      IStudentRepository.cs          # modified — add `search` param to GetPagedAsync signature
      StudentService.cs              # modified — thread `search` through GetStudentsAsync

  SchoolMgmt.Infrastructure/
    Persistence/
      Repositories/
        StudentRepository.cs         # modified — ILIKE filter in GetPagedAsync

  SchoolMgmt.WebApi/
    Controllers/
      StudentsController.cs          # modified — add [FromQuery] string? search param

frontend/src/
  api/
    students.ts                      # new — DTOs, STUDENT_KEYS, studentsApi

  pages/
    admin/
      students/
        StudentsPage.tsx             # new — table, tabs, search, pagination, modals
        components/
          CreateStudentModal.tsx     # new — RHF + Zod create form
          EditStudentModal.tsx       # new — RHF + Zod edit form, fetches full StudentDto
        __tests__/
          StudentsPage.test.tsx      # new
          CreateStudentModal.test.tsx # new
          EditStudentModal.test.tsx  # new
      index.tsx                      # modified — add students route

  layouts/
    AppShell.tsx                     # modified — add Students nav item
```

---

## Testing Strategy

Same stack: Vitest + React Testing Library, `vi.mock` for API modules, per-test `QueryClient`.

### Backend integration tests

The search parameter is covered by two new cases added to `StudentsControllerTests.cs`:

| Test | Setup | Assert |
|---|---|---|
| `GET /api/students?search=nguyen` matches by first name | Seed students "Nguyen Van A" and "Tran Thi B" | Returns only the Nguyen student |
| `GET /api/students?search=2025-000` matches by code prefix | Seed two students with codes `2025-000001`, `2025-000002` | Returns both; a `?search=2025-000001` returns only one |
| `GET /api/students?search=xyz` no match | Seeded students don't match "xyz" | Returns empty `items` array, `totalCount: 0` |

### Frontend unit tests

#### `StudentsPage.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Renders table with student rows | `studentsApi.list` returns 2 students | Both names visible in rows |
| Empty state when no students match | `studentsApi.list` returns `{ items: [], totalCount: 0 }` | "No students found." cell visible |
| Clicking tab changes `status` query param | Render, click "Transferred" tab | `studentsApi.list` re-called with `status: 'Transferred'` |
| Search input debounces | Type "nguyen", advance timers 300 ms | `studentsApi.list` called with `search: 'nguyen'` |
| Pagination Prev/Next buttons | `totalCount: 45`, `pageSize: 20` | Next enabled on page 1; Prev disabled on page 1; Next disabled on page 3 |
| Clicking Edit button opens edit modal | Click pencil button on a row | `EditStudentModal` rendered with correct `studentId` |
| Clicking "Add Student" opens create modal | Click header button | `CreateStudentModal` rendered with `open={true}` |

#### `CreateStudentModal.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Submits correct payload without guardian fields | Fill required fields, submit | `studentsApi.create` called with correct shape; guardian fields absent |
| Submits with guardian fields | Fill all fields including guardian | Guardian fields present in payload |
| Validation blocks empty first name | Submit without filling first name | Error message rendered; `studentsApi.create` not called |
| Disables submit while pending | Mock mutation in-flight | Submit button disabled |
| Closes and resets on success | Mock successful mutation | `onClose` called; form fields cleared |

#### `EditStudentModal.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Shows loading state while fetching | `studentsApi.getById` pending | Spinner visible, fields not rendered |
| Populates form from fetched student | `studentsApi.getById` returns full `StudentDto` | Fields pre-filled with student data |
| StudentCode shown as read-only text | Fetch returns `studentCode: '2025-000001'` | "2025-000001" visible; no input element with that value |
| EnrollmentStatus dropdown present | Fetch succeeds | Status select visible with current value |
| Submits correct UpdateStudentRequest | Fill form, submit | `studentsApi.update` called with all fields including `enrollmentStatus`; no `studentCode` in payload |

All tests mock `studentsApi` at the module boundary — no real HTTP calls.

---

## Commands

```bash
# Install new shadcn components (run from frontend/)
npx shadcn@latest add table select tabs

# Frontend dev server
npm run dev

# Frontend tests
npm run test

# Frontend build (TypeScript check)
npm run build

# Backend build + all tests
dotnet test SchoolMgmt.slnx
```

---

## Boundaries

**Always:**
- Invalidate `['students']` (the prefix key) after any successful mutation — `invalidateQueries({ queryKey: ['students'] })` catches both `list` and `detail` keys
- Keep `StudentCode` out of the `UpdateStudentRequest` payload — it must never be sent on PUT
- Use `keepPreviousData` on the list query to prevent table flicker on page/filter changes
- Reset `page` to `1` whenever `tab` or `debouncedSearch` changes — stale page numbers on a filtered result set are confusing
- Use `enabled: studentId !== null` on the `getById` query — do not fire a GET with an empty string ID

**Ask first:**
- Adding a dedicated student detail page at `/admin/students/:id` (currently out of scope; all info is in the edit modal)
- Adding client-side sorting to table columns (requires backend `?sortBy=` / `?sortDir=` params)
- Replacing `window.confirm` with `AlertDialog` for destructive actions (upgrade all pages together if upgrading)

**Never:**
- Send `studentCode` in a `PUT /api/students/{id}` body — it is permanently immutable
- Add a Delete button — the backend has no DELETE route and students are never hard-deleted
- Skip the `enabled` guard on `getById` — a blank ID would fire `GET /api/students/` which resolves to the list endpoint

---

## Success Criteria

- `GET /admin/students` (logged in as Admin) renders the Students table with no console errors; unauthenticated requests redirect to `/login`
- Status tabs filter the list correctly: clicking "Transferred" shows only transferred students
- Typing in the search box triggers a debounced API call with `?search=`; results update after 300 ms
- Prev/Next pagination buttons advance and retreat pages; "Page X of Y" label is accurate; Prev is disabled on page 1
- Clicking "Add Student" opens the create modal; submitting a valid form creates a student and the list refreshes
- The create modal form has no `EnrollmentStatus` field
- Clicking the edit pencil opens the edit modal pre-populated with the student's current data including guardian fields
- The edit modal displays `StudentCode` as read-only text (not an input); it is absent from the PUT payload
- Changing `EnrollmentStatus` to "Transferred" in the edit modal causes the student to disappear from the Active tab after save
- All unit tests pass (`npm run test` from `frontend/`)
- All backend integration tests pass (`dotnet test SchoolMgmt.slnx`)
- `npm run build` succeeds with no TypeScript errors
